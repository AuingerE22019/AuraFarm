using System.Security.Claims;
using AuraFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace AuraFarm.Api.Controllers;

/// <summary>Authenticated member self-service (profile + emergency contacts).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "member")]
public sealed class MemberPortalController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connString = configuration.GetConnectionString("AuraFarm")
        ?? throw new InvalidOperationException("No Connection String");

    private Guid? GetMemberId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var id) ? id : null;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT member_id, first_name, last_name, username, email, phone,
                   date_of_birth, is_verified_student, registration_date, address_id
            FROM Members WHERE member_id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", memberId.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return NotFound();

        var dto = new
        {
            memberId = reader.GetGuid(0),
            firstName = reader.IsDBNull(1) ? null : reader.GetString(1),
            lastName = reader.IsDBNull(2) ? null : reader.GetString(2),
            username = reader.IsDBNull(3) ? null : reader.GetString(3),
            email = reader.IsDBNull(4) ? null : reader.GetString(4),
            phone = reader.IsDBNull(5) ? null : reader.GetString(5),
            dateOfBirth = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6).ToString("yyyy-MM-dd"),
            isVerifiedStudent = reader.IsDBNull(7) ? (bool?)null : reader.GetBoolean(7),
            registrationDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
            addressId = reader.IsDBNull(9) ? (Guid?)null : reader.GetGuid(9),
        };
        await reader.CloseAsync();
        return Ok(dto);
    }

    public sealed record EmergencyContactRow(
        Guid ContactId,
        string? FirstName,
        string? LastName,
        string? PhoneNumber,
        string? Email,
        string? Relation,
        int? Priority);

    [HttpGet("emergency-contacts")]
    public async Task<ActionResult<List<EmergencyContactRow>>> ListEmergencyContacts(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        var list = new List<EmergencyContactRow>();
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT ec.contact_id, ec.first_name, ec.last_name, ec.phone_number, ec.email,
                   mec.relation, mec.priority
            FROM Member_Emergency_Contacts mec
            JOIN Emergency_Contacts ec ON ec.contact_id = mec.contact_id
            WHERE mec.member_id = @mid
            ORDER BY mec.priority NULLS LAST, ec.last_name, ec.first_name
            """,
            conn);
        cmd.Parameters.AddWithValue("mid", memberId.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new EmergencyContactRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return Ok(list);
    }

    public sealed record AddEmergencyContactRequest(
        string FirstName,
        string? LastName,
        string PhoneNumber,
        string? Email,
        string? Relation,
        int Priority);

    [HttpPost("emergency-contacts")]
    public async Task<IActionResult> AddEmergencyContact([FromBody] AddEmergencyContactRequest req, CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.PhoneNumber))
            return BadRequest(new { message = "First name and phone are required." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            Guid contactId;
            await using (var insEc = new NpgsqlCommand(
                       """
                       INSERT INTO Emergency_Contacts (first_name, last_name, phone_number, email)
                       VALUES (@fn, @ln, @phone, @email)
                       RETURNING contact_id
                       """,
                       conn, tx))
            {
                insEc.Parameters.AddWithValue("fn", req.FirstName.Trim());
                insEc.Parameters.AddWithValue("ln", (object?)req.LastName?.Trim() ?? DBNull.Value);
                insEc.Parameters.AddWithValue("phone", req.PhoneNumber.Trim());
                insEc.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(req.Email) ? (object)DBNull.Value : req.Email!.Trim());

                var scalar = await insEc.ExecuteScalarAsync(ct);
                if (scalar is null) return BadRequest(new { message = "Could not create emergency contact." });
                contactId = (Guid)scalar;
            }

            await using (var link = new NpgsqlCommand(
                       """
                       INSERT INTO Member_Emergency_Contacts (member_id, contact_id, relation, priority)
                       VALUES (@mid, @cid, @rel, @pri)
                       """,
                       conn, tx))
            {
                link.Parameters.AddWithValue("mid", memberId.Value);
                link.Parameters.AddWithValue("cid", contactId);
                link.Parameters.AddWithValue("rel", (object?)req.Relation?.Trim() ?? DBNull.Value);
                link.Parameters.AddWithValue("pri", NpgsqlDbType.Integer, req.Priority <= 0 ? 1 : req.Priority);
                await link.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Ok(new { contactId });
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = ex.MessageText });
        }
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return Ok(new { hasContract = false });

        await ApplyContractTransitionsAsync(conn, contractId.Value, ct);
        var dto = await LoadSubscriptionAsync(conn, contractId.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("subscription/pause")]
    public async Task<IActionResult> PauseSubscription(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return NotFound(new { message = "Kein Vertrag gefunden." });

        await ApplyContractTransitionsAsync(conn, contractId.Value, ct);
        var today = ContractBilling.Today();
        var state = await ReadContractStateAsync(conn, contractId.Value, ct);
        if (state is null) return NotFound();

        if (state.Status == "terminated")
            return BadRequest(new { message = "Vertrag ist bereits beendet." });
        if (today > state.CommitmentEnd)
            return BadRequest(new { message = "Vertragslaufzeit ist abgelaufen." });
        if (state.Status == "paused")
            return BadRequest(new { message = "Abo ist bereits pausiert." });
        if (state.PauseEffective is not null)
            return BadRequest(new { message = "Pause ist bereits geplant." });

        var pauseFrom = ContractBilling.FirstOfNextMonth(today);
        var activeUntil = ContractBilling.EndOfMonth(today);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Contracts
            SET pause_effective_date = @pause_from, resume_effective_date = NULL
            WHERE contract_id = @cid
            """,
            conn);
        cmd.Parameters.AddWithValue("pause_from", pauseFrom);
        cmd.Parameters.AddWithValue("cid", contractId.Value);
        await cmd.ExecuteNonQueryAsync(ct);

        return Ok(new
        {
            message = $"Abo bleibt aktiv bis {activeUntil:dd.MM.yyyy}, danach pausiert (ab {pauseFrom:dd.MM.yyyy}).",
            pauseEffectiveDate = pauseFrom.ToString("yyyy-MM-dd"),
            activeThroughDate = activeUntil.ToString("yyyy-MM-dd"),
        });
    }

    [HttpPost("subscription/resume")]
    public async Task<IActionResult> ResumeSubscription(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return NotFound(new { message = "Kein Vertrag gefunden." });

        await ApplyContractTransitionsAsync(conn, contractId.Value, ct);
        var today = ContractBilling.Today();
        var state = await ReadContractStateAsync(conn, contractId.Value, ct);
        if (state is null) return NotFound();

        if (state.Status == "terminated")
            return BadRequest(new { message = "Vertrag ist bereits beendet." });
        if (today > state.CommitmentEnd)
            return BadRequest(new { message = "Vertragslaufzeit ist abgelaufen." });

        if (state.CancelledAt is not null)
        {
            await using var uncancel = new NpgsqlCommand(
                """
                UPDATE Contracts
                SET cancelled_at = NULL, auto_renew = TRUE
                WHERE contract_id = @cid
                """,
                conn);
            uncancel.Parameters.AddWithValue("cid", contractId.Value);
            await uncancel.ExecuteNonQueryAsync(ct);
            return Ok(new { message = "Kündigung zurückgenommen — der Vertrag verlängert sich wieder automatisch." });
        }

        if (state.Status == "active" && state.PauseEffective is not null)
        {
            await using var cancelPause = new NpgsqlCommand(
                "UPDATE Contracts SET pause_effective_date = NULL WHERE contract_id = @cid", conn);
            cancelPause.Parameters.AddWithValue("cid", contractId.Value);
            await cancelPause.ExecuteNonQueryAsync(ct);
            return Ok(new { message = "Geplante Pause wurde abgebrochen." });
        }

        if (state.Status != "paused")
            return BadRequest(new { message = "Abo ist nicht pausiert." });
        if (state.ResumeEffective is not null)
            return BadRequest(new { message = "Reaktivierung ist bereits geplant." });

        var resumeFrom = ContractBilling.FirstOfNextMonth(today);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Contracts
            SET resume_effective_date = @resume_from, pause_effective_date = NULL
            WHERE contract_id = @cid
            """,
            conn);
        cmd.Parameters.AddWithValue("resume_from", resumeFrom);
        cmd.Parameters.AddWithValue("cid", contractId.Value);
        await cmd.ExecuteNonQueryAsync(ct);

        return Ok(new { message = $"Reaktivierung ab {resumeFrom:dd.MM.yyyy}.", resumeEffectiveDate = resumeFrom });
    }

    [HttpPost("subscription/cancel")]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return NotFound(new { message = "Kein Vertrag gefunden." });

        await ApplyContractTransitionsAsync(conn, contractId.Value, ct);
        var state = await ReadContractStateAsync(conn, contractId.Value, ct);
        if (state is null) return NotFound();
        if (state.CancelledAt is not null)
            return BadRequest(new { message = "Vertrag ist bereits gekündigt." });

        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Contracts
            SET cancelled_at = CURRENT_TIMESTAMP, auto_renew = FALSE
            WHERE contract_id = @cid
            """,
            conn);
        cmd.Parameters.AddWithValue("cid", contractId.Value);
        await cmd.ExecuteNonQueryAsync(ct);

        return Ok(new { message = $"Kündigung zum {state.CommitmentEnd:dd.MM.yyyy} wirksam." });
    }

    public sealed record SetRenewalRequest(Guid RenewalTierPriceId, bool AutoRenew, List<Guid>? RenewalAddonPriceIds);

    [HttpPut("subscription/renewal")]
    public async Task<IActionResult> SetRenewal([FromBody] SetRenewalRequest req, CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return NotFound(new { message = "Kein Vertrag gefunden." });

        await ApplyContractTransitionsAsync(conn, contractId.Value, ct);
        var state = await ReadContractStateAsync(conn, contractId.Value, ct);
        if (state is null) return NotFound();
        if (state.CancelledAt is not null)
            return BadRequest(new { message = "Abo kann nur geändert werden, wenn nicht gekündigt. Bitte zuerst „Reaktivieren“ (Kündigung zurücknehmen)." });
        var today = ContractBilling.Today();
        if (today > state.CommitmentEnd)
            return BadRequest(new { message = "Vertragslaufzeit ist abgelaufen." });

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var check = new NpgsqlCommand("SELECT 1 FROM Tier_Prices WHERE price_id = @pid", conn, tx))
            {
                check.Parameters.AddWithValue("pid", req.RenewalTierPriceId);
                var exists = await check.ExecuteScalarAsync(ct);
                if (exists is null) return BadRequest(new { message = "Ungültiges Abo-Modell." });
            }

            var addonIds = req.RenewalAddonPriceIds?.Distinct().ToList() ?? [];
            if (addonIds.Count > 0)
            {
                await using var ac = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM Addon_Prices WHERE addon_price_id = ANY(@ids)", conn, tx);
                ac.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, addonIds.ToArray());
                var cnt = Convert.ToInt32(await ac.ExecuteScalarAsync(ct));
                if (cnt != addonIds.Count) return BadRequest(new { message = "Ungültiges Zusatzpaket." });
            }

            await using (var cmd = new NpgsqlCommand(
                           """
                           UPDATE Contracts
                           SET renewal_price_id = @rid,
                               auto_renew = @renew,
                               renewal_updated_at = NOW(),
                               cancelled_at = CASE WHEN @renew THEN NULL ELSE cancelled_at END
                           WHERE contract_id = @cid
                           """,
                           conn, tx))
            {
                cmd.Parameters.AddWithValue("rid", req.RenewalTierPriceId);
                cmd.Parameters.AddWithValue("renew", req.AutoRenew);
                cmd.Parameters.AddWithValue("cid", contractId.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var del = new NpgsqlCommand(
                           "DELETE FROM Contract_Renewal_Addons WHERE contract_id = @cid", conn, tx))
            {
                del.Parameters.AddWithValue("cid", contractId.Value);
                await del.ExecuteNonQueryAsync(ct);
            }

            foreach (var apId in addonIds)
            {
                await using var ins = new NpgsqlCommand(
                    "INSERT INTO Contract_Renewal_Addons (contract_id, addon_price_id) VALUES (@cid, @apid)",
                    conn, tx);
                ins.Parameters.AddWithValue("cid", contractId.Value);
                ins.Parameters.AddWithValue("apid", apId);
                await ins.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Ok(new { message = req.AutoRenew ? "Folge-Abo gespeichert." : "Keine automatische Verlängerung." });
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = ex.MessageText });
        }
    }

    [HttpGet("subscription/renewal-plan")]
    public async Task<IActionResult> GetRenewalPlan(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var contractId = await GetActiveContractIdAsync(conn, memberId.Value, ct);
        if (contractId is null) return NotFound(new { message = "Kein Vertrag gefunden." });

        var isStudent = false;
        await using (var st = new NpgsqlCommand(
                       "SELECT COALESCE(is_verified_student, FALSE) FROM Members WHERE member_id = @mid", conn))
        {
            st.Parameters.AddWithValue("mid", memberId.Value);
            var scalar = await st.ExecuteScalarAsync(ct);
            isStudent = scalar is not null && scalar is not DBNull && Convert.ToBoolean(scalar);
        }

        Guid? renewalTierPriceId = null;
        string? renewalCycle = null;
        string? renewalAccess = null;
        await using (var cur = new NpgsqlCommand(
                       """
                       SELECT c.renewal_price_id, tp.billing_cycle::text, t.access_level::text
                       FROM Contracts c
                       JOIN Tier_Prices tp ON tp.price_id = COALESCE(c.renewal_price_id, c.price_id)
                       JOIN Membership_Tiers t ON t.tier_id = tp.tier_id
                       WHERE c.contract_id = @cid
                       """,
                       conn))
        {
            cur.Parameters.AddWithValue("cid", contractId.Value);
            await using var r = await cur.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                renewalTierPriceId = r.GetGuid(0);
                renewalCycle = r.GetString(1);
                renewalAccess = r.GetString(2);
            }
        }

        var renewalAddonPriceIds = new List<Guid>();
        await using (var ra = new NpgsqlCommand(
                       "SELECT addon_price_id FROM Contract_Renewal_Addons WHERE contract_id = @cid", conn))
        {
            ra.Parameters.AddWithValue("cid", contractId.Value);
            await using var r = await ra.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) renewalAddonPriceIds.Add(r.GetGuid(0));
        }

        var tierPrices = await LoadTierPricesCatalogAsync(conn, ct);
        var addonPrices = await LoadAddonPricesCatalogAsync(conn, ct);

        return Ok(new
        {
            tierPrices,
            addonPrices,
            isVerifiedStudent = isStudent,
            current = new
            {
                tierPriceId = renewalTierPriceId,
                addonPriceIds = renewalAddonPriceIds,
                billingCycle = renewalCycle ?? "monthly",
                accessLevel = renewalAccess ?? "home_only",
            },
        });
    }

    [HttpGet("subscription/renewal-options")]
    public Task<IActionResult> RenewalOptions(CancellationToken ct) => GetRenewalPlan(ct);

    private static async Task<List<object>> LoadTierPricesCatalogAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<object>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT tp.price_id, tp.billing_cycle::text, tp.monthly_amount, tp.currency,
                   t.tier_id, t.tier_name, t.access_level::text
            FROM Tier_Prices tp
            JOIN Membership_Tiers t ON t.tier_id = tp.tier_id
            WHERE tp.end_date IS NULL
            ORDER BY t.access_level, tp.billing_cycle
            """,
            conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new
            {
                tierPriceId = r.GetGuid(0),
                billingCycle = r.GetString(1),
                amount = r.GetDecimal(2),
                currency = r.IsDBNull(3) ? "EUR" : r.GetString(3),
                tierId = r.GetGuid(4),
                tierName = r.IsDBNull(5) ? null : r.GetString(5),
                accessLevel = r.IsDBNull(6) ? null : r.GetString(6),
            });
        }
        return list;
    }

    private static async Task<List<object>> LoadAddonPricesCatalogAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var list = new List<object>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT ap.addon_price_id, ap.billing_cycle::text, ap.amount, ap.currency,
                   a.addon_id, a.addon_name, a.is_combo,
                   a.includes_sauna, a.includes_solarium, a.includes_drinks, a.includes_coffee
            FROM Addon_Prices ap
            JOIN Addon_Packages a ON a.addon_id = ap.addon_id
            ORDER BY (a.includes_sauna AND a.includes_solarium AND a.includes_drinks AND a.includes_coffee) DESC,
                     a.is_combo DESC, a.addon_name, ap.billing_cycle
            """,
            conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new
            {
                addonPriceId = r.GetGuid(0),
                billingCycle = r.GetString(1),
                amount = r.GetDecimal(2),
                currency = r.IsDBNull(3) ? "EUR" : r.GetString(3),
                addonId = r.GetGuid(4),
                addonName = r.IsDBNull(5) ? null : r.GetString(5),
                isCombo = !r.IsDBNull(6) && r.GetBoolean(6),
                includesSauna = !r.IsDBNull(7) && r.GetBoolean(7),
                includesSolarium = !r.IsDBNull(8) && r.GetBoolean(8),
                includesDrinks = !r.IsDBNull(9) && r.GetBoolean(9),
                includesCoffee = !r.IsDBNull(10) && r.GetBoolean(10),
            });
        }
        return list;
    }

    private static decimal ToMonthlyPayment(decimal amount, string billingCycle) =>
        billingCycle == "annually"
            ? decimal.Round(amount / 12m, 2, MidpointRounding.AwayFromZero)
            : amount;

    private static string AccessLevelLabel(string? accessLevel) => accessLevel switch
    {
        "home_only" => "Local — nur Heimat-Standort",
        "national" => "National — alle Standorte im Land",
        "global" => "Global — alle Standorte (AT + DE)",
        _ => accessLevel ?? "—",
    };

    private sealed record ContractState(
        string Status,
        DateOnly CommitmentEnd,
        DateOnly? PauseEffective,
        DateOnly? ResumeEffective,
        DateTime? CancelledAt,
        bool AutoRenew,
        string BillingCycle);

    private static async Task<Guid?> GetActiveContractIdAsync(NpgsqlConnection conn, Guid memberId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT contract_id FROM Contracts
            WHERE member_id = @mid AND status <> 'terminated'
            ORDER BY start_date DESC
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("mid", memberId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is Guid g ? g : null;
    }

    private static async Task<ContractState?> ReadContractStateAsync(NpgsqlConnection conn, Guid contractId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT status::text, commitment_end_date, pause_effective_date, resume_effective_date,
                   cancelled_at, auto_renew, billing_cycle::text
            FROM Contracts WHERE contract_id = @cid
            """,
            conn);
        cmd.Parameters.AddWithValue("cid", contractId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new ContractState(
            r.GetString(0),
            r.GetFieldValue<DateOnly>(1),
            r.IsDBNull(2) ? null : r.GetFieldValue<DateOnly>(2),
            r.IsDBNull(3) ? null : r.GetFieldValue<DateOnly>(3),
            r.IsDBNull(4) ? null : r.GetDateTime(4),
            r.GetBoolean(5),
            r.IsDBNull(6) ? "monthly" : r.GetString(6));
    }

    private static async Task ApplyContractTransitionsAsync(NpgsqlConnection conn, Guid contractId, CancellationToken ct)
    {
        var today = ContractBilling.Today();
        var state = await ReadContractStateAsync(conn, contractId, ct);
        if (state is null) return;

        if (state.Status == "active" && state.PauseEffective is { } pe && today >= pe)
        {
            await using var cmd = new NpgsqlCommand(
                "UPDATE Contracts SET status = 'paused', pause_effective_date = NULL WHERE contract_id = @cid",
                conn);
            cmd.Parameters.AddWithValue("cid", contractId);
            await cmd.ExecuteNonQueryAsync(ct);
            state = await ReadContractStateAsync(conn, contractId, ct);
            if (state is null) return;
        }

        if (state.Status == "paused" && state.ResumeEffective is { } re && today >= re)
        {
            await using var cmd = new NpgsqlCommand(
                "UPDATE Contracts SET status = 'active', resume_effective_date = NULL WHERE contract_id = @cid",
                conn);
            cmd.Parameters.AddWithValue("cid", contractId);
            await cmd.ExecuteNonQueryAsync(ct);
            state = await ReadContractStateAsync(conn, contractId, ct);
            if (state is null) return;
        }

        if (state.CancelledAt is not null && today > state.CommitmentEnd && state.Status != "terminated")
        {
            await using var cmd = new NpgsqlCommand(
                """
                UPDATE Contracts
                SET status = 'terminated', end_date = @end
                WHERE contract_id = @cid
                """,
                conn);
            cmd.Parameters.AddWithValue("end", state.CommitmentEnd);
            cmd.Parameters.AddWithValue("cid", contractId);
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        if (state.AutoRenew && state.CancelledAt is null && today > state.CommitmentEnd && state.Status != "terminated")
        {
            var newEnd = state.BillingCycle == "annually"
                ? ContractBilling.EndOfMonth(state.CommitmentEnd.AddDays(1).AddMonths(11))
                : ContractBilling.EndOfMonth(today);
            await using var cmd = new NpgsqlCommand(
                "UPDATE Contracts SET commitment_end_date = @end WHERE contract_id = @cid",
                conn);
            cmd.Parameters.AddWithValue("end", newEnd);
            cmd.Parameters.AddWithValue("cid", contractId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<object?> LoadSubscriptionAsync(NpgsqlConnection conn, Guid contractId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT c.contract_id, c.status::text, c.start_date, c.commitment_end_date, c.final_monthly_rate, c.currency,
                   c.billing_cycle::text, c.auto_renew, c.cancelled_at, c.pause_effective_date, c.resume_effective_date,
                   c.price_id, c.renewal_price_id, c.renewal_updated_at,
                   t.tier_name, t.access_level::text,
                   tp.billing_cycle::text,
                   rt.tier_name, rl.name, rl.city
            FROM Contracts c
            JOIN Tier_Prices tp ON tp.price_id = c.price_id
            JOIN Membership_Tiers t ON t.tier_id = tp.tier_id
            LEFT JOIN Tier_Prices rtp ON rtp.price_id = c.renewal_price_id
            LEFT JOIN Membership_Tiers rt ON rt.tier_id = rtp.tier_id
            LEFT JOIN Members m ON m.member_id = c.member_id
            LEFT JOIN Locations rl ON rl.location_id = m.home_location_id
            WHERE c.contract_id = @cid
            """,
            conn);
        cmd.Parameters.AddWithValue("cid", contractId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        var today = ContractBilling.Today();
        var contractIdOut = r.GetGuid(0);
        var status = r.GetString(1);
        var startDate = r.GetFieldValue<DateOnly>(2);
        var commitmentEnd = r.GetFieldValue<DateOnly>(3);
        var monthlyRate = r.GetDecimal(4);
        var currency = r.IsDBNull(5) ? "EUR" : r.GetString(5);
        var billingCycle = r.IsDBNull(6) ? "monthly" : r.GetString(6);
        var autoRenew = r.GetBoolean(7);
        var cancelledAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8);
        var pauseEff = r.IsDBNull(9) ? (DateOnly?)null : r.GetFieldValue<DateOnly>(9);
        var resumeEff = r.IsDBNull(10) ? (DateOnly?)null : r.GetFieldValue<DateOnly>(10);
        var priceId = r.GetGuid(11);
        var renewalPriceId = r.IsDBNull(12) ? (Guid?)null : r.GetGuid(12);
        var renewalUpdatedAt = r.IsDBNull(13) ? (DateTime?)null : r.GetDateTime(13);
        var tierName = r.IsDBNull(14) ? null : r.GetString(14);
        var accessLevel = r.IsDBNull(15) ? null : r.GetString(15);
        var renewalTierName = r.IsDBNull(17) ? null : r.GetString(17);
        var locName = r.IsDBNull(18) ? null : r.GetString(18);
        var locCity = r.IsDBNull(19) ? null : r.GetString(19);

        var firstBillingMonth = ContractBilling.FirstFullBillingMonth(startDate);
        decimal? lastPayAmount = null;
        string? lastPayPeriodEnd = null;
        await r.CloseAsync();

        await using (var payCmd = new NpgsqlCommand(
                       """
                       SELECT amount, billing_period_end
                       FROM Payments
                       WHERE contract_id = @cid
                       ORDER BY payment_date DESC
                       LIMIT 1
                       """,
                       conn))
        {
            payCmd.Parameters.AddWithValue("cid", contractId);
            await using var pr = await payCmd.ExecuteReaderAsync(ct);
            if (await pr.ReadAsync(ct))
            {
                lastPayAmount = pr.GetDecimal(0);
                lastPayPeriodEnd = pr.GetFieldValue<DateOnly>(1).ToString("yyyy-MM-dd");
            }
        }

        var addons = new List<object>();
        var currentAddonLines = new List<(string? Name, decimal Monthly)>();
        await using (var aCmd = new NpgsqlCommand(
                       """
                       SELECT a.addon_name, ap.billing_cycle::text, ap.amount
                       FROM Contract_Addons ca
                       JOIN Addon_Prices ap ON ap.addon_price_id = ca.addon_price_id
                       JOIN Addon_Packages a ON a.addon_id = ap.addon_id
                       WHERE ca.contract_id = @cid
                       """,
                       conn))
        {
            aCmd.Parameters.AddWithValue("cid", contractId);
            await using var ar = await aCmd.ExecuteReaderAsync(ct);
            while (await ar.ReadAsync(ct))
            {
                var cycle = ar.GetString(1);
                var amt = ar.GetDecimal(2);
                var monthly = cycle == "annually" ? decimal.Round(amt / 12m, 2) : amt;
                var name = ar.IsDBNull(0) ? null : ar.GetString(0);
                currentAddonLines.Add((name, monthly));
                addons.Add(new { addonName = name, monthlyRate = monthly });
            }
        }

        var phase = status switch
        {
            "paused" => "paused",
            _ when pauseEff is not null && pauseEff > today => "pause_scheduled",
            _ when resumeEff is not null && resumeEff > today => "resume_scheduled",
            _ when cancelledAt is not null => "cancelled_running",
            _ => "active",
        };

        var activeThrough = pauseEff is not null && pauseEff > today ? ContractBilling.EndOfMonth(today) : (DateOnly?)null;

        string phaseLabel = phase switch
        {
            "paused" => "Pausiert",
            "pause_scheduled" => $"Aktiv bis {activeThrough:dd.MM.yyyy}, danach pausiert",
            "resume_scheduled" => $"Pausiert — Reaktivierung ab {resumeEff:dd.MM.yyyy}",
            "cancelled_running" => $"Gekündigt — läuft bis {commitmentEnd:dd.MM.yyyy}",
            _ => "Aktiv",
        };

        var renewalAddons = new List<object>();
        var renewalAddonLines = new List<(string? Name, decimal Monthly)>();
        decimal? renewalBaseMonthlyRate = null;
        decimal renewalAddonsMonthlyTotal = 0m;
        string? renewalBillingCycle = null;
        string? renewalBillingCycleLabel = null;
        string? renewalAccessLevel = null;
        var renewalTierNamePlan = renewalTierName;
        await using (var renCmd = new NpgsqlCommand(
                       """
                       SELECT rt.tier_name, rt.access_level::text, rtp.billing_cycle::text, rtp.monthly_amount
                       FROM Contracts c
                       JOIN Tier_Prices rtp ON rtp.price_id = COALESCE(c.renewal_price_id, c.price_id)
                       JOIN Membership_Tiers rt ON rt.tier_id = rtp.tier_id
                       WHERE c.contract_id = @cid
                       """,
                       conn))
        {
            renCmd.Parameters.AddWithValue("cid", contractId);
            await using var rr = await renCmd.ExecuteReaderAsync(ct);
            if (await rr.ReadAsync(ct))
            {
                renewalTierNamePlan = rr.IsDBNull(0) ? renewalTierName : rr.GetString(0);
                renewalAccessLevel = rr.IsDBNull(1) ? null : rr.GetString(1);
                renewalBillingCycle = rr.GetString(2);
                renewalBaseMonthlyRate = ToMonthlyPayment(rr.GetDecimal(3), renewalBillingCycle);
                renewalBillingCycleLabel = renewalBillingCycle == "annually"
                    ? "12 Monate Mindestlaufzeit"
                    : "Flexibel (monatlich kündbar)";
            }
        }

        var currentAddonPriceIds = new HashSet<Guid>();
        await using (var caIdCmd = new NpgsqlCommand(
                       "SELECT addon_price_id FROM Contract_Addons WHERE contract_id = @cid", conn))
        {
            caIdCmd.Parameters.AddWithValue("cid", contractId);
            await using var car = await caIdCmd.ExecuteReaderAsync(ct);
            while (await car.ReadAsync(ct)) currentAddonPriceIds.Add(car.GetGuid(0));
        }

        var renewalAddonPriceIds = new HashSet<Guid>();
        var renewalEffectiveFrom = ContractBilling.RenewalEffectiveFrom(commitmentEnd);

        await using (var raCmd = new NpgsqlCommand(
                       """
                       SELECT cra.addon_price_id, a.addon_name, ap.billing_cycle::text, ap.amount
                       FROM Contract_Renewal_Addons cra
                       JOIN Addon_Prices ap ON ap.addon_price_id = cra.addon_price_id
                       JOIN Addon_Packages a ON a.addon_id = ap.addon_id
                       WHERE cra.contract_id = @cid
                       """,
                       conn))
        {
            raCmd.Parameters.AddWithValue("cid", contractId);
            await using var ar = await raCmd.ExecuteReaderAsync(ct);
            while (await ar.ReadAsync(ct))
            {
                renewalAddonPriceIds.Add(ar.GetGuid(0));
                var cycle = ar.GetString(2);
                var amt = ar.GetDecimal(3);
                var m = ToMonthlyPayment(amt, cycle);
                var name = ar.IsDBNull(1) ? null : ar.GetString(1);
                renewalAddonLines.Add((name, m));
                renewalAddons.Add(new { addonName = name, monthlyRate = m });
                renewalAddonsMonthlyTotal += m;
            }
        }

        var renewalConfigured = renewalUpdatedAt is not null;
        var renewalPlanAddonLines = renewalAddonLines.Count > 0
            ? renewalAddonLines
            : renewalConfigured
                ? []
                : currentAddonLines;
        var renewalPlanAddons = renewalPlanAddonLines
            .Select(x => (object)new { addonName = x.Name, monthlyRate = x.Monthly })
            .ToList();
        if (!renewalConfigured && renewalAddonLines.Count == 0)
            renewalAddonsMonthlyTotal = currentAddonLines.Sum(x => x.Monthly);

        var renewalMonthlyRate = (renewalBaseMonthlyRate ?? 0m) + renewalAddonsMonthlyTotal;
        var renewalPlanChanged = renewalConfigured
            && ((renewalPriceId ?? priceId) != priceId
                || renewalBillingCycle != billingCycle
                || !renewalAddonPriceIds.SetEquals(currentAddonPriceIds));
        var showRenewalPlan = renewalConfigured || (autoRenew && cancelledAt is null);
        var renewalPlanApplies = autoRenew && cancelledAt is null;

        var renewalBreakdown = new List<object>
        {
            new { label = "Abo-Modell", value = AccessLevelLabel(renewalAccessLevel), emphasis = false },
            new { label = "Laufzeit", value = renewalBillingCycleLabel ?? "—", emphasis = false },
            new { label = "Basis-Abo", value = $"{renewalBaseMonthlyRate ?? 0m:N2} € / Monat", emphasis = false },
        };
        foreach (var (name, monthly) in renewalPlanAddonLines)
        {
            renewalBreakdown.Add(new
            {
                label = name ?? "Zusatzpaket",
                value = $"+{monthly:N2} € / Monat",
                emphasis = false,
            });
        }
        renewalBreakdown.Add(new { label = "Gesamt", value = $"{renewalMonthlyRate:N2} € / Monat", emphasis = true });

        var renewalLabel = cancelledAt is not null
            ? $"Endet am {commitmentEnd:dd.MM.yyyy} (keine Verlängerung)"
            : autoRenew
                ? $"Automatisch ab {renewalEffectiveFrom:dd.MM.yyyy}"
                : "Keine automatische Verlängerung";

        return new
        {
            hasContract = true,
            contractId = contractIdOut,
            status,
            phase,
            phaseLabel,
            startDate = startDate.ToString("yyyy-MM-dd"),
            commitmentEndDate = commitmentEnd.ToString("yyyy-MM-dd"),
            firstFullBillingDate = firstBillingMonth.ToString("yyyy-MM-dd"),
            monthlyRate,
            currency,
            billingCycle,
            billingCycleLabel = billingCycle == "annually"
                ? "12 Monate Mindestlaufzeit (1.–1., monatliche Zahlung)"
                : "Flexibel (monatlich kündbar)",
            autoRenew,
            renewalLabel,
            cancelledAt = cancelledAt?.ToString("o"),
            pauseEffectiveDate = pauseEff?.ToString("yyyy-MM-dd"),
            resumeEffectiveDate = resumeEff?.ToString("yyyy-MM-dd"),
            activeThroughDate = activeThrough?.ToString("yyyy-MM-dd"),
            nextBillingDate = ContractBilling.NextBillingDate(today, status, pauseEff, resumeEff).ToString("yyyy-MM-dd"),
            lastPaymentAmount = lastPayAmount,
            lastPaymentPeriodEnd = lastPayPeriodEnd,
            tierName,
            accessLevel,
            renewalTierName = renewalTierNamePlan,
            renewalAccessLevel,
            renewalBillingCycle,
            renewalMonthlyRate,
            renewalBillingCycleLabel,
            renewalAddons,
            renewalEffectiveFrom = renewalEffectiveFrom.ToString("yyyy-MM-dd"),
            renewalPlanChanged,
            showRenewalPlan,
            renewalPlan = new
            {
                effectiveFrom = renewalEffectiveFrom.ToString("yyyy-MM-dd"),
                effectiveFromLabel = $"Gültig ab {renewalEffectiveFrom:dd.MM.yyyy} (erster Zahltag nach Laufzeitende)",
                title = renewalConfigured ? "Geplantes Folge-Abo" : "Folge-Abo bei Verlängerung",
                configurationLabel = renewalConfigured
                    ? "So hast du dein Folge-Abo konfiguriert"
                    : "Entspricht deinem aktuellen Abo (noch nicht angepasst)",
                customized = renewalConfigured,
                configuredAt = renewalUpdatedAt?.ToString("o"),
                homeLocation = locName is null ? null : $"{locCity} — {locName}",
                autoRenew,
                tierName = renewalTierNamePlan,
                accessLevel = renewalAccessLevel,
                accessLevelLabel = AccessLevelLabel(renewalAccessLevel),
                billingCycle = renewalBillingCycle,
                billingCycleLabel = renewalBillingCycleLabel,
                baseMonthlyRate = renewalBaseMonthlyRate,
                addonsMonthlyTotal = renewalAddonsMonthlyTotal,
                monthlyRate = renewalMonthlyRate,
                addons = renewalPlanAddons,
                breakdown = renewalBreakdown,
                applies = renewalPlanApplies,
                changed = renewalPlanChanged,
            },
            homeLocation = locName is null ? null : $"{locCity} — {locName}",
            addons,
            canPause = status != "terminated" && today <= commitmentEnd && status != "paused" && pauseEff is null,
            canResume = status != "terminated" && today <= commitmentEnd
                && (cancelledAt is not null
                    || (status == "paused" && resumeEff is null)
                    || (status == "active" && pauseEff is not null)),
            canUndoCancel = cancelledAt is not null && status != "terminated" && today <= commitmentEnd,
            canCancel = cancelledAt is null && status != "terminated" && today <= commitmentEnd,
            canChangeRenewal = cancelledAt is null && status != "terminated" && today <= commitmentEnd,
        };
    }

    public sealed record MemberSessionRow(
        Guid SessionId,
        string ClassTitle,
        string? Difficulty,
        DateTime StartTime,
        DateTime EndTime,
        string? LocationName,
        string? City,
        string? RoomName,
        string? TrainerName,
        int MaxParticipants,
        int BookedCount,
        int SpotsLeft,
        bool IsCancelled,
        bool IsBookedByMe,
        Guid? MyBookingId);

    public sealed record MemberBookingRow(
        Guid BookingId,
        Guid SessionId,
        string ClassTitle,
        DateTime StartTime,
        DateTime EndTime,
        string? LocationName,
        string? RoomName,
        string? TrainerName,
        string Status);

    private async Task<(Guid? HomeLocationId, string? AccessLevel, string? HomeCountry)?> GetMemberAccessAsync(
        NpgsqlConnection conn, Guid memberId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT m.home_location_id, COALESCE(t.access_level::text, 'home_only'), hl.country_iso
            FROM Members m
            LEFT JOIN Locations hl ON hl.location_id = m.home_location_id
            LEFT JOIN LATERAL (
                SELECT tp.tier_id
                FROM Contracts c
                JOIN Tier_Prices tp ON tp.price_id = c.price_id
                WHERE c.member_id = m.member_id AND c.status = 'active'
                ORDER BY c.start_date DESC
                LIMIT 1
            ) ac ON TRUE
            LEFT JOIN Membership_Tiers t ON t.tier_id = ac.tier_id
            WHERE m.member_id = @mid
            """,
            conn);
        cmd.Parameters.AddWithValue("mid", memberId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var home = r.IsDBNull(0) ? (Guid?)null : r.GetGuid(0);
        var access = r.IsDBNull(1) ? "home_only" : r.GetString(1);
        var country = r.IsDBNull(2) ? null : r.GetString(2);
        return (home, access, country);
    }

    [HttpGet("sessions/upcoming")]
    public async Task<ActionResult<List<MemberSessionRow>>> ListUpcomingSessions(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var access = await GetMemberAccessAsync(conn, memberId.Value, ct);
        if (access is null) return NotFound();

        var (homeLoc, accessLevel, homeCountry) = access.Value;
        if (homeLoc is null) return Ok(new List<MemberSessionRow>());

        var list = new List<MemberSessionRow>();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT s.session_id, c.title, c.difficulty::text, s.start_time, s.end_time,
                   l.name, l.city, r.room_name, st.first_name, st.last_name,
                   s.max_participants, s.is_cancelled,
                   (SELECT COUNT(*)::int FROM Bookings b WHERE b.session_id = s.session_id AND b.status = 'confirmed'),
                   b.booking_id
            FROM Sessions s
            JOIN Classes c ON c.class_id = s.class_id
            JOIN Locations l ON l.location_id = s.location_id
            JOIN Rooms r ON r.room_id = s.room_id
            JOIN Staff st ON st.staff_id = s.trainer_id
            LEFT JOIN Bookings b ON b.session_id = s.session_id AND b.member_id = @mid AND b.status = 'confirmed'
            WHERE s.end_time >= NOW()
              AND s.is_cancelled = FALSE
              AND (
                (@access = 'global')
                OR (@access = 'national' AND l.country_iso = @country)
                OR (@access = 'home_only' AND s.location_id = @home)
              )
            ORDER BY s.start_time
            LIMIT 50
            """,
            conn);
        cmd.Parameters.AddWithValue("mid", memberId.Value);
        cmd.Parameters.AddWithValue("access", accessLevel ?? "home_only");
        cmd.Parameters.AddWithValue("country", (object?)homeCountry ?? DBNull.Value);
        cmd.Parameters.AddWithValue("home", homeLoc.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var max = reader.GetInt32(10);
            var booked = reader.GetInt32(12);
            var fn = reader.IsDBNull(8) ? "" : reader.GetString(8);
            var ln = reader.IsDBNull(9) ? "" : reader.GetString(9);
            var myBooking = reader.IsDBNull(13) ? (Guid?)null : reader.GetGuid(13);
            list.Add(new MemberSessionRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                $"{fn} {ln}".Trim(),
                max,
                booked,
                Math.Max(0, max - booked),
                reader.GetBoolean(11),
                myBooking is not null,
                myBooking));
        }
        return Ok(list);
    }

    [HttpGet("sessions/my-bookings")]
    public async Task<ActionResult<List<MemberBookingRow>>> ListMyBookings(CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        var list = new List<MemberBookingRow>();
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT b.booking_id, s.session_id, c.title, s.start_time, s.end_time,
                   l.name, r.room_name, st.first_name, st.last_name, b.status::text
            FROM Bookings b
            JOIN Sessions s ON s.session_id = b.session_id
            JOIN Classes c ON c.class_id = s.class_id
            JOIN Locations l ON l.location_id = s.location_id
            JOIN Rooms r ON r.room_id = s.room_id
            JOIN Staff st ON st.staff_id = s.trainer_id
            WHERE b.member_id = @mid AND b.status = 'confirmed' AND s.end_time >= NOW()
            ORDER BY s.start_time
            """,
            conn);
        cmd.Parameters.AddWithValue("mid", memberId.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var fn = reader.IsDBNull(7) ? "" : reader.GetString(7);
            var ln = reader.IsDBNull(8) ? "" : reader.GetString(8);
            list.Add(new MemberBookingRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                $"{fn} {ln}".Trim(),
                reader.GetString(9)));
        }
        return Ok(list);
    }

    [HttpPost("sessions/{sessionId:guid}/book")]
    public async Task<IActionResult> BookSession(Guid sessionId, CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        var access = await GetMemberAccessAsync(conn, memberId.Value, ct);
        if (access is null) return NotFound();
        var (homeLoc, accessLevel, homeCountry) = access.Value;

        await using var check = new NpgsqlCommand(
            """
            SELECT s.max_participants, s.is_cancelled, s.start_time, s.location_id, l.country_iso,
                   (SELECT COUNT(*) FROM Bookings b WHERE b.session_id = s.session_id AND b.status = 'confirmed'),
                   (SELECT 1 FROM Bookings b WHERE b.session_id = s.session_id AND b.member_id = @mid AND b.status = 'confirmed')
            FROM Sessions s
            JOIN Locations l ON l.location_id = s.location_id
            WHERE s.session_id = @sid
            """,
            conn);
        check.Parameters.AddWithValue("sid", sessionId);
        check.Parameters.AddWithValue("mid", memberId.Value);
        await using var r = await check.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return NotFound(new { message = "Termin nicht gefunden." });

        var max = r.GetInt32(0);
        var cancelled = r.GetBoolean(1);
        var start = r.GetDateTime(2);
        var locId = r.GetGuid(3);
        var country = r.IsDBNull(4) ? null : r.GetString(4);
        var booked = Convert.ToInt32(r.GetInt64(5));
        var already = !r.IsDBNull(6);
        await r.CloseAsync();

        if (cancelled) return BadRequest(new { message = "Termin wurde abgesagt." });
        if (start <= DateTime.UtcNow) return BadRequest(new { message = "Termin hat bereits begonnen." });
        if (already) return BadRequest(new { message = "Bereits eingetragen." });
        if (booked >= max) return BadRequest(new { message = "Keine Plätze mehr frei." });

        var allowed = accessLevel switch
        {
            "global" => true,
            "national" => country == homeCountry,
            _ => homeLoc is not null && locId == homeLoc.Value,
        };
        if (!allowed) return BadRequest(new { message = "Mit deinem Abo ist dieser Standort nicht buchbar." });

        // Re-activate prior cancellation (UNIQUE member_id + session_id blocks a second INSERT).
        await using (var rebook = new NpgsqlCommand(
                       """
                       UPDATE Bookings
                       SET status = 'confirmed', booked_at = NOW()
                       WHERE member_id = @mid AND session_id = @sid AND status = 'cancelled'
                       RETURNING booking_id
                       """,
                       conn))
        {
            rebook.Parameters.AddWithValue("mid", memberId.Value);
            rebook.Parameters.AddWithValue("sid", sessionId);
            var reactivated = await rebook.ExecuteScalarAsync(ct);
            if (reactivated is Guid rebookId)
                return Ok(new { bookingId = rebookId, message = "Erfolgreich eingetragen." });
        }

        await using var ins = new NpgsqlCommand(
            """
            INSERT INTO Bookings (member_id, session_id, booked_at, status)
            VALUES (@mid, @sid, NOW(), 'confirmed')
            RETURNING booking_id
            """,
            conn);
        ins.Parameters.AddWithValue("mid", memberId.Value);
        ins.Parameters.AddWithValue("sid", sessionId);
        try
        {
            var bookingId = (Guid)(await ins.ExecuteScalarAsync(ct))!;
            return Ok(new { bookingId, message = "Erfolgreich eingetragen." });
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return BadRequest(new { message = "Bereits eingetragen." });
        }
    }

    [HttpDelete("sessions/bookings/{bookingId:guid}")]
    public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken ct)
    {
        var memberId = GetMemberId();
        if (memberId is null) return Unauthorized();

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE Bookings b
            SET status = 'cancelled'
            FROM Sessions s
            WHERE b.booking_id = @bid AND b.member_id = @mid AND b.session_id = s.session_id
              AND b.status = 'confirmed' AND s.start_time > NOW()
            """,
            conn);
        cmd.Parameters.AddWithValue("bid", bookingId);
        cmd.Parameters.AddWithValue("mid", memberId.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
            return BadRequest(new { message = "Abmeldung nicht möglich (nicht gefunden oder Termin läuft bereits)." });
        return Ok(new { message = "Abmeldung gespeichert." });
    }
}

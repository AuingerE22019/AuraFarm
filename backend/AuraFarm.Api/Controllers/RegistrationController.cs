using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using AuraFarm.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RegistrationController : ControllerBase
{
    private readonly string _connString;

    public RegistrationController(IConfiguration configuration)
    {
        _connString = configuration.GetConnectionString("AuraFarm") ?? throw new InvalidOperationException("No Connection String");
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Staff registers a member with minimal data; member completes profile after first login.</summary>
    public sealed record RegisterMemberRequest(string FirstName, string LastName, bool IsVerifiedStudent);

    [HttpPost("staff/members")]
    [Authorize(Roles = "admin,manager,receptionist")]
    public async Task<IActionResult> RegisterMember([FromBody] RegisterMemberRequest req)
    {
        var staffIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(staffIdString, out var staffId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First and last name are required." });

        var random = new Random();
        var tempUsername = $"{req.FirstName.ToLowerInvariant().Trim()}{req.LastName.ToLowerInvariant().Trim()}{random.Next(100, 999)}";
        var tempPassword = $"Setup{random.Next(1000, 9999)}!";
        var tempPasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        Guid memberId;
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT member_pkg.register_new_member(@staff_id, @fname, @lname, @user, @pass, @student)",
                conn);
            cmd.Parameters.AddWithValue("staff_id", NpgsqlDbType.Uuid, staffId);
            cmd.Parameters.AddWithValue("fname", req.FirstName.Trim());
            cmd.Parameters.AddWithValue("lname", req.LastName.Trim());
            cmd.Parameters.AddWithValue("user", tempUsername);
            cmd.Parameters.AddWithValue("pass", tempPasswordHash);
            cmd.Parameters.AddWithValue("student", req.IsVerifiedStudent);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null) return BadRequest(new { message = "Registration failed." });
            memberId = (Guid)result;
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { message = ex.MessageText });
        }

        var pdf = GenerateSetupPdf(req.FirstName.Trim(), req.LastName.Trim(), tempUsername, tempPassword);
        return File(pdf, "application/pdf", $"{req.FirstName.Trim()}_{req.LastName.Trim()}_Setup.pdf");
    }

    private static byte[] GenerateQRCode(string text)
    {
        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);
    }

    private static byte[] GenerateSetupPdf(string firstName, string lastName, string username, string password)
    {
        var setupUrl = $"http://localhost:4200/login?setup=true&u={Uri.EscapeDataString(username)}&p={Uri.EscapeDataString(password)}";
        var qrCodeImage = GenerateQRCode(setupUrl);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                page.Header()
                    .Text("Willkommen bei AuraFarm!")
                    .SemiBold().FontSize(24).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(15);
                        x.Item().Text($"Hallo {firstName} {lastName},").Bold().FontSize(14);
                        x.Item().Text(
                            "Dein Zugang wurde angelegt. Bitte melde dich mit den temporären Daten an und vervollständige alle persönlichen Angaben (E-Mail, Geburtsdatum, Adresse, Telefon). Notfallkontakte trägst du im Mitglieder-Dashboard ein.");

                        x.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(c =>
                        {
                            c.Spacing(5);
                            c.Item().Text($"Benutzername: {username}").Bold();
                            c.Item().Text($"Temporäres Passwort: {password}").Bold();
                        });

                        x.Item().Text("QR-Code / Link zur ersten Anmeldung:");
                        x.Item().Hyperlink(setupUrl).Text(setupUrl).FontColor(Colors.Blue.Medium).Underline();

                        x.Item().Width(120).Height(120).Image(qrCodeImage);

                        x.Item().PaddingTop(20).Text("Datenschutz & Nutzung").Bold().FontSize(12);
                        x.Item().Text(
                                "Mit der ersten Anmeldung akzeptierst du die Hausordnung. Datenverarbeitung gemäß DSGVO für Mitgliedschaft und Abrechnung.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Seite ");
                        x.CurrentPageNumber();
                        x.Span(" von ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();
    }

    public sealed record MemberSetupRequest(
        string OldUsername,
        string OldPassword,
        string NewUsername,
        string NewPassword,
        string Email,
        string PaymentMethod,
        Guid HomeLocationId,
        Guid TierPriceId,
        List<Guid>? AddonPriceIds,
        DateTime DateOfBirth,
        string Phone,
        string Street,
        string? HouseNumber,
        string ZipCode,
        string City,
        string CountryIso);

    public sealed record MemberSetupOptionsRequest(string OldUsername, string OldPassword);

    [HttpPost("member/setup/options")]
    public async Task<IActionResult> SetupOptions([FromBody] MemberSetupOptionsRequest req, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        // Verify temp credentials first.
        Guid memberId;
        Guid? recruiterStaffId = null;
        var isVerifiedStudent = false;
        await using (var verifyCmd = new NpgsqlCommand(
                       """
                       SELECT member_id, password_hash, recruited_by, COALESCE(is_verified_student, FALSE)
                       FROM Members WHERE username = @user OR email = @user
                       """,
                       conn))
        {
            verifyCmd.Parameters.AddWithValue("user", req.OldUsername);
            await using var reader = await verifyCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return Unauthorized(new { message = "Invalid credentials." });
            memberId = reader.GetGuid(0);
            var hash = reader.GetString(1);
            recruiterStaffId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
            isVerifiedStudent = !reader.IsDBNull(3) && reader.GetBoolean(3);
            await reader.CloseAsync();
            if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, hash))
                return Unauthorized(new { message = "Invalid credentials." });
        }

        Guid? defaultLocationId = null;
        if (recruiterStaffId is not null)
        {
            await using var staffCmd = new NpgsqlCommand("SELECT home_location_id FROM Staff WHERE staff_id = @sid", conn);
            staffCmd.Parameters.AddWithValue("sid", recruiterStaffId.Value);
            var scalar = await staffCmd.ExecuteScalarAsync(ct);
            defaultLocationId = scalar is DBNull or null ? null : (Guid?)scalar;
        }

        // Locations grouped by country
        var locations = new Dictionary<string, List<object>>();
        await using (var locCmd = new NpgsqlCommand(
                       "SELECT location_id, name, country_iso, city FROM Locations WHERE is_active = TRUE ORDER BY country_iso, city, name",
                       conn))
        {
            await using var r = await locCmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetGuid(0);
                var name = r.IsDBNull(1) ? null : r.GetString(1);
                var iso = r.IsDBNull(2) ? "" : r.GetString(2);
                var city = r.IsDBNull(3) ? null : r.GetString(3);
                if (!locations.TryGetValue(iso, out var list))
                {
                    list = new List<object>();
                    locations[iso] = list;
                }
                list.Add(new { locationId = id, name, countryIso = iso, city });
            }
        }

        // Tier prices
        var tierPrices = new List<object>();
        await using (var tpCmd = new NpgsqlCommand(
                       """
                       SELECT tp.price_id, tp.billing_cycle::text, tp.monthly_amount, tp.currency,
                              t.tier_id, t.tier_name, t.access_level::text
                       FROM Tier_Prices tp
                       JOIN Membership_Tiers t ON t.tier_id = tp.tier_id
                       WHERE tp.end_date IS NULL
                       ORDER BY t.access_level, tp.billing_cycle
                       """,
                       conn))
        {
            await using var r = await tpCmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                tierPrices.Add(new
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
        }

        // Addon prices
        var addonPrices = new List<object>();
        await using (var apCmd = new NpgsqlCommand(
                       """
                       SELECT ap.addon_price_id, ap.billing_cycle::text, ap.amount, ap.currency,
                              a.addon_id, a.addon_name, a.is_combo,
                              a.includes_sauna, a.includes_solarium, a.includes_drinks, a.includes_coffee
                       FROM Addon_Prices ap
                       JOIN Addon_Packages a ON a.addon_id = ap.addon_id
                       ORDER BY (a.includes_sauna AND a.includes_solarium AND a.includes_drinks AND a.includes_coffee) DESC,
                                a.is_combo DESC, a.addon_name, ap.billing_cycle
                       """,
                       conn))
        {
            await using var r = await apCmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                addonPrices.Add(new
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
        }

        return Ok(new
        {
            memberId,
            defaultLocationId,
            isVerifiedStudent,
            locationsByCountry = locations,
            tierPrices,
            addonPrices
        });
    }

    [HttpPost("member/setup")]
    public async Task<IActionResult> SetupMember([FromBody] MemberSetupRequest req, CancellationToken ct)
    {
        if (req.HomeLocationId == Guid.Empty)
            return BadRequest(new { message = "Bitte einen Standort auswählen." });
        if (req.TierPriceId == Guid.Empty)
            return BadRequest(new { message = "Bitte ein Abo-Modell und die Laufzeit wählen." });
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { message = "Bitte eine gültige E-Mail-Adresse angeben." });
        if (string.IsNullOrWhiteSpace(req.NewUsername) || req.NewUsername.Trim().Length < 3)
            return BadRequest(new { message = "Username muss mindestens 3 Zeichen haben." });

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        await using var verifyCmd = new NpgsqlCommand(
            "SELECT member_id, password_hash FROM Members WHERE username = @user OR email = @user",
            conn);
        verifyCmd.Parameters.AddWithValue("user", req.OldUsername);

        await using var reader = await verifyCmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync()) return Unauthorized(new { message = "Invalid credentials." });

        var memberId = reader.GetGuid(0);
        var hash = reader.GetString(1);
        await reader.CloseAsync();

        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, hash))
            return Unauthorized(new { message = "Invalid credentials." });

        if (req.DateOfBirth >= DateTime.UtcNow.Date.AddYears(-14))
            return BadRequest(new { message = "Member must be at least 14 years old." });

        var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        var dob = DateOnly.FromDateTime(req.DateOfBirth.Date);

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            Guid addressId;
            await using (var addrCmd = new NpgsqlCommand(
                       """
                       INSERT INTO Addresses (street, house_number, zip_code, city, country_iso)
                       VALUES (@street, @house, @zip, @city, @country)
                       RETURNING address_id
                       """,
                       conn, tx))
            {
                addrCmd.Parameters.AddWithValue("street", req.Street.Trim());
                addrCmd.Parameters.AddWithValue("house", (object?)req.HouseNumber?.Trim() ?? DBNull.Value);
                addrCmd.Parameters.AddWithValue("zip", req.ZipCode.Trim());
                addrCmd.Parameters.AddWithValue("city", req.City.Trim());
                var iso = (req.CountryIso ?? "AT").Trim().ToUpperInvariant();
                if (iso.Length != 2) iso = "AT";
                addrCmd.Parameters.AddWithValue("country", iso);

                var scalar = await addrCmd.ExecuteScalarAsync(ct);
                if (scalar is null) throw new InvalidOperationException("Address insert failed.");
                addressId = (Guid)scalar;
            }

            await using (var updateCmd = new NpgsqlCommand(
                       """
                       UPDATE Members
                       SET username = @new_user,
                           password_hash = @new_pass,
                           email = @email,
                           home_location_id = @home_loc,
                           date_of_birth = @dob,
                           phone = @phone,
                           address_id = @addr
                       WHERE member_id = @id
                       """,
                       conn, tx))
            {
                updateCmd.Parameters.AddWithValue("new_user", req.NewUsername.Trim());
                updateCmd.Parameters.AddWithValue("new_pass", newHash);
                updateCmd.Parameters.AddWithValue("email", req.Email.Trim());
                updateCmd.Parameters.AddWithValue("home_loc", NpgsqlDbType.Uuid, req.HomeLocationId);
                updateCmd.Parameters.AddWithValue("dob", NpgsqlDbType.Date, dob);
                updateCmd.Parameters.AddWithValue("phone", req.Phone.Trim());
                updateCmd.Parameters.AddWithValue("addr", addressId);
                updateCmd.Parameters.AddWithValue("id", memberId);

                var n = await updateCmd.ExecuteNonQueryAsync(ct);
                if (n != 1) throw new InvalidOperationException("Member update failed.");
            }

            // Create contract + optional add-ons (mock / demo)
            Guid contractId;
            decimal tierAmount;
            string tierCurrency;
            string tierCycle;
            await using (var priceCmd = new NpgsqlCommand(
                           "SELECT billing_cycle::text, monthly_amount, currency FROM Tier_Prices WHERE price_id = @pid",
                           conn, tx))
            {
                priceCmd.Parameters.AddWithValue("pid", NpgsqlDbType.Uuid, req.TierPriceId);
                await using var r = await priceCmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) return BadRequest(new { message = "Invalid tier price selected." });
                tierCycle = r.GetString(0);
                tierAmount = ToMonthlyPayment(r.GetDecimal(1), tierCycle);
                tierCurrency = r.IsDBNull(2) ? "EUR" : r.GetString(2);
            }

            decimal addonsTotal = 0m;
            var addonIds = req.AddonPriceIds?.Distinct().ToList() ?? new List<Guid>();
            if (addonIds.Count > 0)
            {
                await using var sumCmd = new NpgsqlCommand(
                    """
                    SELECT COALESCE(SUM(
                      CASE WHEN billing_cycle = 'annually' THEN amount / 12.0 ELSE amount END
                    ), 0)
                    FROM Addon_Prices WHERE addon_price_id = ANY(@ids)
                    """,
                    conn, tx);
                sumCmd.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, addonIds.ToArray());
                var scalar = await sumCmd.ExecuteScalarAsync(ct);
                addonsTotal = scalar is null ? 0m : Convert.ToDecimal(scalar);
            }

            var isStudent = false;
            await using (var stCmd = new NpgsqlCommand(
                           "SELECT COALESCE(is_verified_student, FALSE) FROM Members WHERE member_id = @mid",
                           conn, tx))
            {
                stCmd.Parameters.AddWithValue("mid", NpgsqlDbType.Uuid, memberId);
                var st = await stCmd.ExecuteScalarAsync(ct);
                isStudent = st is not null && st is not DBNull && Convert.ToBoolean(st);
            }

            var gross = tierAmount + addonsTotal;
            var finalRate = isStudent ? decimal.Round(gross * 0.9m, 2, MidpointRounding.AwayFromZero) : gross;

            var signupDate = ContractBilling.Today();
            var commitmentEnd = ContractBilling.InitialCommitmentEnd(signupDate, tierCycle);
            var proratedPay = ContractBilling.ProratedSignupAmount(finalRate, signupDate);
            var periodEnd = ContractBilling.EndOfMonth(signupDate);

            await using (var insContract = new NpgsqlCommand(
                           """
                           INSERT INTO Contracts (
                             member_id, price_id, start_date, final_monthly_rate, currency, status,
                             billing_cycle, commitment_end_date, auto_renew, renewal_price_id)
                           VALUES (
                             @mid, @pid, @start, @rate, @cur, 'active',
                             @cycle::tier_cycle, @commit_end, TRUE, @pid)
                           RETURNING contract_id
                           """,
                           conn, tx))
            {
                insContract.Parameters.AddWithValue("mid", NpgsqlDbType.Uuid, memberId);
                insContract.Parameters.AddWithValue("pid", NpgsqlDbType.Uuid, req.TierPriceId);
                insContract.Parameters.AddWithValue("start", signupDate);
                insContract.Parameters.AddWithValue("rate", finalRate);
                insContract.Parameters.AddWithValue("cur", tierCurrency);
                insContract.Parameters.AddWithValue("cycle", tierCycle);
                insContract.Parameters.AddWithValue("commit_end", commitmentEnd);
                var scalar = await insContract.ExecuteScalarAsync(ct);
                if (scalar is null) throw new InvalidOperationException("Contract insert failed.");
                contractId = (Guid)scalar;
            }

            if (addonIds.Count > 0)
            {
                foreach (var apId in addonIds)
                {
                    await using var link = new NpgsqlCommand(
                        "INSERT INTO Contract_Addons (contract_id, addon_price_id) VALUES (@cid, @apid) ON CONFLICT DO NOTHING",
                        conn, tx);
                    link.Parameters.AddWithValue("cid", NpgsqlDbType.Uuid, contractId);
                    link.Parameters.AddWithValue("apid", NpgsqlDbType.Uuid, apId);
                    await link.ExecuteNonQueryAsync(ct);
                }
            }

            // Create a mock initial payment row (paid) for demo purposes
            var method = (req.PaymentMethod ?? "card").Trim().ToLowerInvariant();
            await using (var payCmd = new NpgsqlCommand(
                           """
                           INSERT INTO Payments (contract_id, amount, payment_date, billing_period_start, billing_period_end, method, status)
                           VALUES (@cid, @amt, CURRENT_TIMESTAMP, @pstart, @pend, @method::payment_method, 'paid')
                           """,
                           conn, tx))
            {
                payCmd.Parameters.AddWithValue("cid", NpgsqlDbType.Uuid, contractId);
                payCmd.Parameters.AddWithValue("amt", proratedPay);
                payCmd.Parameters.AddWithValue("pstart", signupDate);
                payCmd.Parameters.AddWithValue("pend", periodEnd);
                payCmd.Parameters.AddWithValue("method", method);
                await payCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync();
            return Ok(new
            {
                message = "Setup complete",
                proratedAmount = proratedPay,
                proratedThrough = periodEnd.ToString("yyyy-MM-dd"),
                nextBillingDate = ContractBilling.FirstFullBillingMonth(signupDate).ToString("yyyy-MM-dd"),
            });
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            var msg = ex.SqlState switch
            {
                "23505" => "Username oder E-Mail ist bereits vergeben.",
                "23514" => ex.ConstraintName switch
                {
                    "members_email_check" => "E-Mail-Format ist ungültig.",
                    "members_date_of_birth_check" => "Du musst mindestens 14 Jahre alt sein.",
                    "members_username_check" => "Username: mind. 3 Zeichen, keine Leerzeichen.",
                    _ => ex.MessageText,
                },
                _ => ex.MessageText,
            };
            return BadRequest(new { message = msg });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return BadRequest(new { message = ex.Message });
        }
    }

    private static decimal ToMonthlyPayment(decimal amount, string billingCycle) =>
        billingCycle == "annually"
            ? decimal.Round(amount / 12m, 2, MidpointRounding.AwayFromZero)
            : amount;
}

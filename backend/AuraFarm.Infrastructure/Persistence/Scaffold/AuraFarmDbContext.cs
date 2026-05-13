using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AuraFarm.Infrastructure.Persistence.Scaffold;

public partial class AuraFarmDbContext : DbContext
{
    public AuraFarmDbContext(DbContextOptions<AuraFarmDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<_class> classes { get; set; }

    public virtual DbSet<address> addresses { get; set; }

    public virtual DbSet<booking> bookings { get; set; }

    public virtual DbSet<contract> contracts { get; set; }

    public virtual DbSet<discount> discounts { get; set; }

    public virtual DbSet<emergency_contact> emergency_contacts { get; set; }

    public virtual DbSet<equipment> equipment { get; set; }

    public virtual DbSet<location> locations { get; set; }

    public virtual DbSet<member> members { get; set; }

    public virtual DbSet<member_emergency_contact> member_emergency_contacts { get; set; }

    public virtual DbSet<membership_tier> membership_tiers { get; set; }

    public virtual DbSet<payment> payments { get; set; }

    public virtual DbSet<room> rooms { get; set; }

    public virtual DbSet<session> sessions { get; set; }

    public virtual DbSet<staff> staff { get; set; }

    public virtual DbSet<tier_price> tier_prices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("access_level", new[] { "home_only", "national", "global" })
            .HasPostgresEnum("booking_status", new[] { "confirmed", "cancelled", "attended", "no_show" })
            .HasPostgresEnum("contract_status", new[] { "active", "paused", "terminated", "pending" })
            .HasPostgresEnum("difficulty_level", new[] { "beginner", "intermediate", "advanced", "pro" })
            .HasPostgresEnum("equipment_status", new[] { "operational", "under_repair", "retired", "broken" })
            .HasPostgresEnum("floor_material", new[] { "rubber", "wood", "turf", "mats", "concrete" })
            .HasPostgresEnum("payment_method", new[] { "card", "transfer", "cash", "direct_debit" })
            .HasPostgresEnum("payment_status", new[] { "paid", "failed", "refunded" })
            .HasPostgresEnum("tier_cycle", new[] { "monthly", "annually" })
            .HasPostgresEnum("user_role", new[] { "trainer", "manager", "admin", "cleaner", "receptionist" })
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<_class>(entity =>
        {
            entity.HasKey(e => e.class_id).HasName("classes_pkey");

            entity.Property(e => e.class_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.title).HasMaxLength(100);
        });

        modelBuilder.Entity<address>(entity =>
        {
            entity.HasKey(e => e.address_id).HasName("addresses_pkey");

            entity.Property(e => e.address_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.city).HasMaxLength(100);
            entity.Property(e => e.country_iso)
                .HasMaxLength(2)
                .IsFixedLength();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.house_number).HasMaxLength(20);
            entity.Property(e => e.state_province).HasMaxLength(100);
            entity.Property(e => e.street).HasMaxLength(150);
            entity.Property(e => e.zip_code).HasMaxLength(20);
        });

        modelBuilder.Entity<booking>(entity =>
        {
            entity.HasKey(e => e.booking_id).HasName("bookings_pkey");

            entity.HasIndex(e => new { e.member_id, e.session_id }, "bookings_member_id_session_id_key").IsUnique();

            entity.Property(e => e.booking_id).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.member).WithMany(p => p.bookings)
                .HasForeignKey(d => d.member_id)
                .HasConstraintName("bookings_member_id_fkey");

            entity.HasOne(d => d.session).WithMany(p => p.bookings)
                .HasForeignKey(d => d.session_id)
                .HasConstraintName("bookings_session_id_fkey");
        });

        modelBuilder.Entity<contract>(entity =>
        {
            entity.HasKey(e => e.contract_id).HasName("contracts_pkey");

            entity.Property(e => e.contract_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.currency)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.final_monthly_rate).HasPrecision(10, 2);

            entity.HasOne(d => d.discount).WithMany(p => p.contracts)
                .HasForeignKey(d => d.discount_id)
                .HasConstraintName("contracts_discount_id_fkey");

            entity.HasOne(d => d.member).WithMany(p => p.contracts)
                .HasForeignKey(d => d.member_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("contracts_member_id_fkey");

            entity.HasOne(d => d.price).WithMany(p => p.contracts)
                .HasForeignKey(d => d.price_id)
                .HasConstraintName("contracts_price_id_fkey");
        });

        modelBuilder.Entity<discount>(entity =>
        {
            entity.HasKey(e => e.discount_id).HasName("discounts_pkey");

            entity.Property(e => e.discount_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.category).HasMaxLength(20);
            entity.Property(e => e.fixed_off).HasPrecision(10, 2);
            entity.Property(e => e.name).HasMaxLength(50);
            entity.Property(e => e.percent_off).HasPrecision(5, 2);
        });

        modelBuilder.Entity<emergency_contact>(entity =>
        {
            entity.HasKey(e => e.contact_id).HasName("emergency_contacts_pkey");

            entity.Property(e => e.contact_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.first_name).HasMaxLength(50);
            entity.Property(e => e.last_name).HasMaxLength(50);
            entity.Property(e => e.phone_number).HasMaxLength(20);
        });

        modelBuilder.Entity<equipment>(entity =>
        {
            entity.HasKey(e => e.equipment_id).HasName("equipment_pkey");

            entity.HasIndex(e => e.serial_number, "equipment_serial_number_key").IsUnique();

            entity.Property(e => e.equipment_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.brand_model).HasMaxLength(100);
            entity.Property(e => e.serial_number).HasMaxLength(50);

            entity.HasOne(d => d.room).WithMany(p => p.equipment)
                .HasForeignKey(d => d.room_id)
                .HasConstraintName("equipment_room_id_fkey");
        });

        modelBuilder.Entity<location>(entity =>
        {
            entity.HasKey(e => e.location_id).HasName("locations_pkey");

            entity.Property(e => e.location_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.city).HasMaxLength(100);
            entity.Property(e => e.country_iso)
                .HasMaxLength(2)
                .IsFixedLength();
            entity.Property(e => e.name).HasMaxLength(100);
            entity.Property(e => e.timezone).HasMaxLength(50);

            entity.HasOne(d => d.address).WithMany(p => p.locations)
                .HasForeignKey(d => d.address_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("locations_address_id_fkey");

            entity.HasOne(d => d.manager).WithMany(p => p.locations)
                .HasForeignKey(d => d.manager_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_manager");
        });

        modelBuilder.Entity<member>(entity =>
        {
            entity.HasKey(e => e.member_id).HasName("members_pkey");

            entity.HasIndex(e => e.email, "members_email_key").IsUnique();

            entity.HasIndex(e => e.username, "members_username_key").IsUnique();

            entity.Property(e => e.member_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.first_name).HasMaxLength(50);
            entity.Property(e => e.last_name).HasMaxLength(50);
            entity.Property(e => e.phone).HasMaxLength(20);
            entity.Property(e => e.username).HasMaxLength(50);

            entity.HasOne(d => d.address).WithMany(p => p.members)
                .HasForeignKey(d => d.address_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("members_address_id_fkey");

            entity.HasOne(d => d.home_location).WithMany(p => p.members)
                .HasForeignKey(d => d.home_location_id)
                .HasConstraintName("members_home_location_id_fkey");
        });

        modelBuilder.Entity<member_emergency_contact>(entity =>
        {
            entity.HasKey(e => new { e.member_id, e.contact_id }).HasName("member_emergency_contacts_pkey");

            entity.Property(e => e.relation).HasMaxLength(50);

            entity.HasOne(d => d.contact).WithMany(p => p.member_emergency_contacts)
                .HasForeignKey(d => d.contact_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("member_emergency_contacts_contact_id_fkey");

            entity.HasOne(d => d.member).WithMany(p => p.member_emergency_contacts)
                .HasForeignKey(d => d.member_id)
                .HasConstraintName("member_emergency_contacts_member_id_fkey");
        });

        modelBuilder.Entity<membership_tier>(entity =>
        {
            entity.HasKey(e => e.tier_id).HasName("membership_tiers_pkey");

            entity.HasIndex(e => e.tier_name, "membership_tiers_tier_name_key").IsUnique();

            entity.Property(e => e.tier_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.tier_name).HasMaxLength(50);
        });

        modelBuilder.Entity<payment>(entity =>
        {
            entity.HasKey(e => e.payment_id).HasName("payments_pkey");

            entity.Property(e => e.payment_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.amount).HasPrecision(10, 2);

            entity.HasOne(d => d.contract).WithMany(p => p.payments)
                .HasForeignKey(d => d.contract_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("payments_contract_id_fkey");
        });

        modelBuilder.Entity<room>(entity =>
        {
            entity.HasKey(e => e.room_id).HasName("rooms_pkey");

            entity.Property(e => e.room_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.room_name).HasMaxLength(50);

            entity.HasOne(d => d.location).WithMany(p => p.rooms)
                .HasForeignKey(d => d.location_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("rooms_location_id_fkey");
        });

        modelBuilder.Entity<session>(entity =>
        {
            entity.HasKey(e => e.session_id).HasName("sessions_pkey");

            entity.Property(e => e.session_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.is_cancelled).HasDefaultValue(false);

            entity.HasOne(d => d._class).WithMany(p => p.sessions)
                .HasForeignKey(d => d.class_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("sessions_class_id_fkey");

            entity.HasOne(d => d.location).WithMany(p => p.sessions)
                .HasForeignKey(d => d.location_id)
                .HasConstraintName("sessions_location_id_fkey");

            entity.HasOne(d => d.room).WithMany(p => p.sessions)
                .HasForeignKey(d => d.room_id)
                .HasConstraintName("sessions_room_id_fkey");

            entity.HasOne(d => d.trainer).WithMany(p => p.sessions)
                .HasForeignKey(d => d.trainer_id)
                .HasConstraintName("sessions_trainer_id_fkey");
        });

        modelBuilder.Entity<staff>(entity =>
        {
            entity.HasKey(e => e.staff_id).HasName("staff_pkey");

            entity.HasIndex(e => e.email, "staff_email_key").IsUnique();

            entity.HasIndex(e => e.username, "staff_username_key").IsUnique();

            entity.Property(e => e.staff_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.first_name).HasMaxLength(50);
            entity.Property(e => e.last_name).HasMaxLength(50);
            entity.Property(e => e.specialization).HasMaxLength(100);
            entity.Property(e => e.username).HasMaxLength(50);

            entity.HasOne(d => d.address).WithMany(p => p.staff)
                .HasForeignKey(d => d.address_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("staff_address_id_fkey");

            entity.HasOne(d => d.home_location).WithMany(p => p.staff)
                .HasForeignKey(d => d.home_location_id)
                .HasConstraintName("staff_home_location_id_fkey");
        });

        modelBuilder.Entity<tier_price>(entity =>
        {
            entity.HasKey(e => e.price_id).HasName("tier_prices_pkey");

            entity.Property(e => e.price_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'EUR'::bpchar")
                .IsFixedLength();
            entity.Property(e => e.monthly_amount).HasPrecision(10, 2);

            entity.HasOne(d => d.tier).WithMany(p => p.tier_prices)
                .HasForeignKey(d => d.tier_id)
                .HasConstraintName("tier_prices_tier_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

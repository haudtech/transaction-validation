using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Migrations;

[DbContext(typeof(GatewayPolicyDbContext))]
public sealed class GatewayPolicyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientEntity", entity =>
        {
            entity.Property<string>("ClientId").HasMaxLength(128).HasColumnType("character varying(128)");
            entity.Property<bool>("Enabled").HasColumnType("boolean");
            entity.HasKey("ClientId");
            entity.ToTable("GatewayClients");
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientApiGrantEntity", entity =>
        {
            entity.Property<string>("ClientId").HasMaxLength(128).HasColumnType("character varying(128)");
            entity.Property<string>("ApiId").HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("ApiVersion").HasMaxLength(32).HasColumnType("character varying(32)");
            entity.HasKey("ClientId", "ApiId", "ApiVersion");
            entity.ToTable("GatewayClientApiGrants");
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientKeyEntity", entity =>
        {
            entity.Property<string>("KeyId").HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("ClientId").IsRequired().HasMaxLength(128).HasColumnType("character varying(128)");
            entity.Property<byte[]>("Verifier").IsRequired().HasColumnType("bytea");
            entity.Property<string>("PepperVersion").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<GatewayClientKeyStatus>("Status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnType("character varying(16)");
            entity.Property<DateTimeOffset>("NotBefore").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("ExpiresAt").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("RevokedAt").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("PreviousSince").HasColumnType("timestamp with time zone");
            entity.HasKey("KeyId");
            entity.HasIndex("ClientId", "Status");
            entity.HasIndex(new[] { "ClientId" }, "IX_GatewayClientKeys_ClientId_Current")
                .IsUnique()
                .HasFilter("\"Status\" = 'Current'");
            entity.HasIndex(new[] { "ClientId" }, "IX_GatewayClientKeys_ClientId_Previous")
                .IsUnique()
                .HasFilter("\"Status\" = 'Previous'");
            entity.ToTable("GatewayClientKeys", table =>
            {
                table.HasCheckConstraint("CK_GatewayClientKeys_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.HasCheckConstraint("CK_GatewayClientKeys_Status", "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_PreviousWindow",
                    "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");
                table.HasCheckConstraint("CK_GatewayClientKeys_Validity", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
            });
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayBackendCredentialEntity", entity =>
        {
            entity.Property<string>("KeyId").HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("ApiId").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<byte[]>("Verifier").IsRequired().HasColumnType("bytea");
            entity.Property<string>("PepperVersion").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("SecretName").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("Status").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            entity.Property<DateTimeOffset>("NotBefore").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("ExpiresAt").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("RevokedAt").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset?>("PreviousSince").HasColumnType("timestamp with time zone");
            entity.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
            entity.HasKey("KeyId");
            entity.HasIndex("ApiId", "KeyId");
            entity.HasIndex(new[] { "ApiId" }, "IX_GatewayBackendCredentials_ApiId_Current")
                .IsUnique()
                .HasFilter("\"Status\" = 'Current'");
            entity.HasIndex(new[] { "ApiId" }, "IX_GatewayBackendCredentials_ApiId_Previous")
                .IsUnique()
                .HasFilter("\"Status\" = 'Previous'");
            entity.ToTable("GatewayBackendCredentials", table =>
            {
                table.HasCheckConstraint("CK_GatewayBackendCredentials_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.HasCheckConstraint("CK_GatewayBackendCredentials_Status", "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_PreviousWindow",
                    "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");
                table.HasCheckConstraint("CK_GatewayBackendCredentials_Validity", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
            });
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayAuditEventEntity", entity =>
        {
            entity.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);
            entity.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
            entity.Property<string>("AdminIssuer").IsRequired().HasMaxLength(512).HasColumnType("character varying(512)");
            entity.Property<string>("AdminSubject").IsRequired().HasMaxLength(256).HasColumnType("character varying(256)");
            entity.Property<string>("AdminClientId").HasMaxLength(256).HasColumnType("character varying(256)");
            entity.Property<string>("Action").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            entity.Property<string>("Target").IsRequired().HasMaxLength(256).HasColumnType("character varying(256)");
            entity.Property<string>("Outcome").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
            entity.HasKey("Id");
            entity.HasIndex("OccurredAt");
            entity.ToTable("GatewayAuditEvents");
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientApiGrantEntity", entity =>
        {
            entity.HasOne("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientEntity", "Client")
                .WithMany("ApiGrants")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            entity.Navigation("Client");
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientKeyEntity", entity =>
        {
            entity.HasOne("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientEntity", "Client")
                .WithMany("Keys")
                .HasForeignKey("ClientId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
            entity.Navigation("Client");
        });

        modelBuilder.Entity("TransactionValidation.Gateway.PolicyStore.Entities.GatewayClientEntity", entity =>
        {
            entity.Navigation("ApiGrants");
            entity.Navigation("Keys");
        });
    }
}

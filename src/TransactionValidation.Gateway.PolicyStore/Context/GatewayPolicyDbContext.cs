using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts.Models;
using TransactionValidation.Gateway.PolicyStore.Entities;

namespace TransactionValidation.Gateway.PolicyStore.Context;

public sealed class GatewayPolicyDbContext(DbContextOptions<GatewayPolicyDbContext> options) : DbContext(options)
{
    public DbSet<GatewayClientEntity> Clients => Set<GatewayClientEntity>();

    public DbSet<GatewayClientKeyEntity> ClientKeys => Set<GatewayClientKeyEntity>();

    public DbSet<GatewayClientApiGrantEntity> ClientApiGrants => Set<GatewayClientApiGrantEntity>();

    public DbSet<GatewayBackendCredentialEntity> BackendCredentials => Set<GatewayBackendCredentialEntity>();

    public DbSet<GatewayAuditEventEntity> AuditEvents => Set<GatewayAuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GatewayClientEntity>(entity =>
        {
            entity.ToTable("GatewayClients");
            entity.HasKey(client => client.ClientId);
            entity.Property(client => client.ClientId).HasMaxLength(128);
            entity.Property(client => client.Enabled).IsRequired();
        });

        modelBuilder.Entity<GatewayClientKeyEntity>(entity =>
        {
            entity.ToTable("GatewayClientKeys", table =>
            {
                table.HasCheckConstraint("CK_GatewayClientKeys_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_Status",
                    "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_PreviousWindow",
                    "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayClientKeys_Validity",
                    "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
            });
            entity.HasKey(key => key.KeyId);
            entity.Property(key => key.KeyId).HasMaxLength(64);
            entity.Property(key => key.ClientId).HasMaxLength(128).IsRequired();
            entity.Property(key => key.Verifier).HasColumnType("bytea").IsRequired();
            entity.Property(key => key.PepperVersion).HasMaxLength(64).IsRequired();
            entity.Property(key => key.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(key => key.NotBefore).HasColumnType("timestamp with time zone");
            entity.Property(key => key.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(key => key.RevokedAt).HasColumnType("timestamp with time zone");
            entity.Property(key => key.PreviousSince).HasColumnType("timestamp with time zone");
            entity.HasIndex(key => new { key.ClientId, key.Status });
            entity.HasOne(key => key.Client)
                .WithMany(client => client.Keys)
                .HasForeignKey(key => key.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(key => key.ClientId, "IX_GatewayClientKeys_ClientId_Current")
                .IsUnique()
                .HasFilter("\"Status\" = 'Current'");
            entity.HasIndex(key => key.ClientId, "IX_GatewayClientKeys_ClientId_Previous")
                .IsUnique()
                .HasFilter("\"Status\" = 'Previous'");
        });

        modelBuilder.Entity<GatewayClientApiGrantEntity>(entity =>
        {
            entity.ToTable("GatewayClientApiGrants");
            entity.HasKey(grant => new { grant.ClientId, grant.ApiId, grant.ApiVersion });
            entity.Property(grant => grant.ClientId).HasMaxLength(128);
            entity.Property(grant => grant.ApiId).HasMaxLength(64);
            entity.Property(grant => grant.ApiVersion).HasMaxLength(32);
            entity.HasOne(grant => grant.Client)
                .WithMany(client => client.ApiGrants)
                .HasForeignKey(grant => grant.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GatewayBackendCredentialEntity>(entity =>
        {
            entity.ToTable("GatewayBackendCredentials", table =>
            {
                table.HasCheckConstraint("CK_GatewayBackendCredentials_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_Status",
                    "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_PreviousWindow",
                    "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_GatewayBackendCredentials_Validity",
                    "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
            });
            entity.HasKey(credential => credential.KeyId);
            entity.Property(credential => credential.KeyId).HasMaxLength(64);
            entity.Property(credential => credential.ApiId).HasMaxLength(64).IsRequired();
            entity.Property(credential => credential.Verifier).HasColumnType("bytea").IsRequired();
            entity.Property(credential => credential.PepperVersion).HasMaxLength(64).IsRequired();
            entity.Property(credential => credential.SecretName).HasMaxLength(64).IsRequired();
            entity.Property(credential => credential.Status).HasMaxLength(16).IsRequired();
            entity.Property(credential => credential.NotBefore).HasColumnType("timestamp with time zone");
            entity.Property(credential => credential.ExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(credential => credential.RevokedAt).HasColumnType("timestamp with time zone");
            entity.Property(credential => credential.PreviousSince).HasColumnType("timestamp with time zone");
            entity.Property(credential => credential.Version).IsConcurrencyToken();
            entity.HasIndex(credential => credential.ApiId, "IX_GatewayBackendCredentials_ApiId_Current")
                .IsUnique()
                .HasFilter("\"Status\" = 'Current'");
            entity.HasIndex(credential => credential.ApiId, "IX_GatewayBackendCredentials_ApiId_Previous")
                .IsUnique()
                .HasFilter("\"Status\" = 'Previous'");
            entity.HasIndex(credential => new { credential.ApiId, credential.KeyId });
        });

        modelBuilder.Entity<GatewayAuditEventEntity>(entity =>
        {
            entity.ToTable("GatewayAuditEvents");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Id).UseIdentityAlwaysColumn();
            entity.Property(audit => audit.OccurredAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(audit => audit.OccurredAt);
            entity.Property(audit => audit.AdminIssuer).HasMaxLength(512).IsRequired();
            entity.Property(audit => audit.AdminSubject).HasMaxLength(256).IsRequired();
            entity.Property(audit => audit.AdminClientId).HasMaxLength(256);
            entity.Property(audit => audit.Action).HasMaxLength(64).IsRequired();
            entity.Property(audit => audit.Target).HasMaxLength(256).IsRequired();
            entity.Property(audit => audit.Outcome).HasMaxLength(32).IsRequired();
        });
    }
}

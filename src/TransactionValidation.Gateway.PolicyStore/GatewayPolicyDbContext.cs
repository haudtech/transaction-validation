using Microsoft.EntityFrameworkCore;

using TransactionValidation.Gateway.Contracts;
using TransactionValidation.Gateway.PolicyStore.Entities;

namespace TransactionValidation.Gateway.PolicyStore;

public sealed class GatewayPolicyDbContext(DbContextOptions<GatewayPolicyDbContext> options) : DbContext(options)
{
    public DbSet<GatewayClientEntity> Clients => Set<GatewayClientEntity>();

    public DbSet<GatewayClientKeyEntity> ClientKeys => Set<GatewayClientKeyEntity>();

    public DbSet<GatewayClientApiGrantEntity> ClientApiGrants => Set<GatewayClientApiGrantEntity>();

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
            entity.HasIndex(key => new { key.ClientId, key.Status });
            entity.HasOne(key => key.Client)
                .WithMany(client => client.Keys)
                .HasForeignKey(key => key.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
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
    }
}

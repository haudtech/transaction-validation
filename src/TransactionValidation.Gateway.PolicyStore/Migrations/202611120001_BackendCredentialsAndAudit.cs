using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Migrations;

[DbContext(typeof(GatewayPolicyDbContext))]
[Migration("202611120001_BackendCredentialsAndAudit")]
public sealed class BackendCredentialsAndAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GatewayBackendCredentials",
            columns: table => new
            {
                KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ApiId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Verifier = table.Column<byte[]>(type: "bytea", nullable: false),
                PepperVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SecretName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                PreviousSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GatewayBackendCredentials", credential => credential.KeyId);
                table.CheckConstraint("CK_GatewayBackendCredentials_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.CheckConstraint(
                    "CK_GatewayBackendCredentials_Status",
                    "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.CheckConstraint(
                    "CK_GatewayBackendCredentials_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.CheckConstraint(
                    "CK_GatewayBackendCredentials_PreviousWindow",
                    "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");
                table.CheckConstraint(
                    "CK_GatewayBackendCredentials_Validity",
                    "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
            });

        migrationBuilder.CreateTable(
            name: "GatewayAuditEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AdminIssuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                AdminSubject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AdminClientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GatewayAuditEvents", audit => audit.Id));

        migrationBuilder.CreateIndex(
            name: "IX_GatewayBackendCredentials_ApiId_Current",
            table: "GatewayBackendCredentials",
            column: "ApiId",
            unique: true,
            filter: "\"Status\" = 'Current'");
        migrationBuilder.CreateIndex(
            name: "IX_GatewayBackendCredentials_ApiId_Previous",
            table: "GatewayBackendCredentials",
            column: "ApiId",
            unique: true,
            filter: "\"Status\" = 'Previous'");
        migrationBuilder.CreateIndex(
            name: "IX_GatewayBackendCredentials_ApiId_KeyId",
            table: "GatewayBackendCredentials",
            columns: new[] { "ApiId", "KeyId" });
        migrationBuilder.CreateIndex(
            name: "IX_GatewayAuditEvents_OccurredAt",
            table: "GatewayAuditEvents",
            column: "OccurredAt");

        migrationBuilder.Sql(
            """
            CREATE FUNCTION "ProtectGatewayAuditEvents"() RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'Gateway audit events are append-only.';
            END;
            $$;

            CREATE TRIGGER "TR_GatewayAuditEvents_AppendOnly"
            BEFORE UPDATE OR DELETE ON "GatewayAuditEvents"
            FOR EACH ROW EXECUTE FUNCTION "ProtectGatewayAuditEvents"();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS "TR_GatewayAuditEvents_AppendOnly" ON "GatewayAuditEvents";
            DROP FUNCTION IF EXISTS "ProtectGatewayAuditEvents"();
            """);
        migrationBuilder.DropTable(name: "GatewayAuditEvents");
        migrationBuilder.DropTable(name: "GatewayBackendCredentials");
    }
}

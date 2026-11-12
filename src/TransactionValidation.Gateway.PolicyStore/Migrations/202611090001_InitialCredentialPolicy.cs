using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Migrations;

[DbContext(typeof(GatewayPolicyDbContext))]
[Migration("202611090001_InitialCredentialPolicy")]
public sealed class InitialCredentialPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GatewayClients",
            columns: table => new
            {
                ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GatewayClients", x => x.ClientId));

        migrationBuilder.CreateTable(
            name: "GatewayClientApiGrants",
            columns: table => new
            {
                ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ApiId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ApiVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GatewayClientApiGrants", x => new { x.ClientId, x.ApiId, x.ApiVersion });
                table.ForeignKey(
                    name: "FK_GatewayClientApiGrants_GatewayClients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "GatewayClients",
                    principalColumn: "ClientId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "GatewayClientKeys",
            columns: table => new
            {
                KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Verifier = table.Column<byte[]>(type: "bytea", nullable: false),
                PepperVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GatewayClientKeys", x => x.KeyId);
                table.CheckConstraint("CK_GatewayClientKeys_VerifierLength", "octet_length(\"Verifier\") = 32");
                table.CheckConstraint("CK_GatewayClientKeys_Status", "\"Status\" IN ('Current', 'Previous', 'Revoked')");
                table.CheckConstraint(
                    "CK_GatewayClientKeys_Revocation",
                    "(\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL) OR (\"Status\" <> 'Revoked' AND \"RevokedAt\" IS NULL)");
                table.CheckConstraint("CK_GatewayClientKeys_Validity", "\"ExpiresAt\" IS NULL OR \"ExpiresAt\" > \"NotBefore\"");
                table.ForeignKey(
                    name: "FK_GatewayClientKeys_GatewayClients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "GatewayClients",
                    principalColumn: "ClientId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GatewayClientKeys_ClientId_Status",
            table: "GatewayClientKeys",
            columns: new[] { "ClientId", "Status" });

        migrationBuilder.Sql(
            """
            CREATE FUNCTION "ProtectGatewayClientKeyIdentity"() RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Gateway client keys cannot be deleted.';
            END IF;

            IF NEW."KeyId" IS DISTINCT FROM OLD."KeyId" THEN
                RAISE EXCEPTION 'Gateway client key IDs cannot be changed.';
            END IF;

            RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "TR_GatewayClientKeys_PreserveKeyId"
            BEFORE DELETE OR UPDATE ON "GatewayClientKeys"
            FOR EACH ROW EXECUTE FUNCTION "ProtectGatewayClientKeyIdentity"();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS "TR_GatewayClientKeys_PreserveKeyId" ON "GatewayClientKeys";
            DROP FUNCTION IF EXISTS "ProtectGatewayClientKeyIdentity"();
            """);
        migrationBuilder.DropTable(name: "GatewayClientApiGrants");
        migrationBuilder.DropTable(name: "GatewayClientKeys");
        migrationBuilder.DropTable(name: "GatewayClients");
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Migrations;

[DbContext(typeof(GatewayPolicyDbContext))]
[Migration("202611110001_ClientKeyAuthenticationPolicy")]
public sealed class ClientKeyAuthenticationPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PreviousSince",
            table: "GatewayClientKeys",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_GatewayClientKeys_PreviousWindow",
            table: "GatewayClientKeys",
            sql: "(\"Status\" = 'Previous' AND \"PreviousSince\" IS NOT NULL AND \"ExpiresAt\" IS NOT NULL AND \"ExpiresAt\" > \"PreviousSince\" AND \"ExpiresAt\" <= \"PreviousSince\" + INTERVAL '1 hour') OR (\"Status\" <> 'Previous' AND \"PreviousSince\" IS NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_GatewayClientKeys_ClientId_Current",
            table: "GatewayClientKeys",
            column: "ClientId",
            unique: true,
            filter: "\"Status\" = 'Current'");

        migrationBuilder.CreateIndex(
            name: "IX_GatewayClientKeys_ClientId_Previous",
            table: "GatewayClientKeys",
            column: "ClientId",
            unique: true,
            filter: "\"Status\" = 'Previous'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GatewayClientKeys_ClientId_Current",
            table: "GatewayClientKeys");

        migrationBuilder.DropIndex(
            name: "IX_GatewayClientKeys_ClientId_Previous",
            table: "GatewayClientKeys");

        migrationBuilder.DropCheckConstraint(
            name: "CK_GatewayClientKeys_PreviousWindow",
            table: "GatewayClientKeys");

        migrationBuilder.DropColumn(
            name: "PreviousSince",
            table: "GatewayClientKeys");
    }
}

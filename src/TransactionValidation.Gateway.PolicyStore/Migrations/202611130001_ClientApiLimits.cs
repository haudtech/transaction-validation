using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using TransactionValidation.Gateway.PolicyStore.Context;

namespace TransactionValidation.Gateway.PolicyStore.Migrations;

[DbContext(typeof(GatewayPolicyDbContext))]
[Migration("202611130001_ClientApiLimits")]
public sealed class ClientApiLimits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GatewayClientApiLimits",
            columns: table => new
            {
                ClientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ApiId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RatePerMinute = table.Column<int>(type: "integer", nullable: false),
                Burst = table.Column<int>(type: "integer", nullable: false),
                DailyQuota = table.Column<long>(type: "bigint", nullable: false),
                MonthlyQuota = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GatewayClientApiLimits", x => new { x.ClientId, x.ApiId });
                table.CheckConstraint("CK_GatewayClientApiLimits_Rate", "\"RatePerMinute\" > 0 AND \"Burst\" > 0");
                table.CheckConstraint(
                    "CK_GatewayClientApiLimits_Quota",
                    "\"DailyQuota\" > 0 AND \"MonthlyQuota\" >= \"DailyQuota\"");
                table.ForeignKey(
                    name: "FK_GatewayClientApiLimits_GatewayClients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "GatewayClients",
                    principalColumn: "ClientId",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "GatewayClientApiLimits");
}

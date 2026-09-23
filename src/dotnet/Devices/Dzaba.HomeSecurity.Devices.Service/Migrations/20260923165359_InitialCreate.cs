using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dzaba.HomeSecurity.Devices.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    AuthMode = table.Column<int>(type: "integer", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRouterBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRouterBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRouterBindings_Routers_RouterId",
                        column: x => x.RouterId,
                        principalTable: "Routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MacAddress = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenViaRouterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Routers_LastSeenViaRouterId",
                        column: x => x.LastSeenViaRouterId,
                        principalTable: "Routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRouterBindings_RouterId",
                table: "AgentRouterBindings",
                column: "RouterId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRouterBindings_TenantId_DeviceCredentialId",
                table: "AgentRouterBindings",
                columns: new[] { "TenantId", "DeviceCredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeenViaRouterId",
                table: "Devices",
                column: "LastSeenViaRouterId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_MacAddress",
                table: "Devices",
                columns: new[] { "TenantId", "MacAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routers_TenantId",
                table: "Routers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRouterBindings");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Routers");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dzaba.HomeSecurity.Org.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SecretHash = table.Column<string>(type: "text", nullable: false),
                    SecretCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCredentials_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Memberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionKey",
                        column: x => x.PermissionKey,
                        principalTable: "Permissions",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.TenantId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Organizations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Key", "Description" },
                values: new object[,]
                {
                    { "audit.view", "View the organization's audit trail" },
                    { "data_subject_request.manage", "Start and complete data export/erasure requests" },
                    { "device_credential.delete", "Delete agent device credentials" },
                    { "device_credential.view", "View agent device credentials" },
                    { "device.manage", "Rename/acknowledge network devices" },
                    { "device.view", "View network devices" },
                    { "logs.view", "View logs" },
                    { "org.manage_members", "Manage organization members" },
                    { "router.manage", "Create, update, and delete routers" },
                    { "router.view", "View router metadata (never the secret)" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Owner", null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Admin", null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Member", null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Viewer", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionKey", "RoleId" },
                values: new object[,]
                {
                    { "audit.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "data_subject_request.manage", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device_credential.delete", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device_credential.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device.manage", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "router.manage", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "router.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "audit.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "data_subject_request.manage", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device_credential.delete", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device_credential.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device.manage", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "router.manage", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "router.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device_credential.view", new Guid("00000000-0000-0000-0000-000000000003") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000003") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000003") },
                    { "device_credential.view", new Guid("00000000-0000-0000-0000-000000000004") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000004") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredentials_TenantId",
                table: "DeviceCredentials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Identifier",
                table: "Organizations",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionKey",
                table: "RolePermissions",
                column: "PermissionKey");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId",
                table: "Roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId",
                table: "UserRoles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCredentials");

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}

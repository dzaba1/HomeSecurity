using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dzaba.HomeSecurity.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedPermissionCatalogAndSystemRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Key", "Description" },
                values: new object[,]
                {
                    { "device.delete", "Delete devices" },
                    { "device.view", "View devices" },
                    { "logs.view", "View logs" },
                    { "org.manage_members", "Manage organization members" }
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
                    { "device.delete", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000001") },
                    { "device.delete", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000002") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000003") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000003") },
                    { "device.view", new Guid("00000000-0000-0000-0000-000000000004") },
                    { "logs.view", new Guid("00000000-0000-0000-0000-000000000004") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.delete", new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.view", new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "logs.view", new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.delete", new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.view", new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "logs.view", new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "org.manage_members", new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.view", new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "logs.view", new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "device.view", new Guid("00000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionKey", "RoleId" },
                keyValues: new object[] { "logs.view", new Guid("00000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "device.delete");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "device.view");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "logs.view");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "org.manage_members");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));
        }
    }
}

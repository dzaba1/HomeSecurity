using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dzaba.HomeSecurity.Audit.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActorPseudonyms",
                columns: table => new
                {
                    ActorType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActorPseudonyms", x => new { x.ActorType, x.ActorId });
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorRef = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Metadata = table.Column<string>(type: "json", nullable: false),
                    PrevHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Hash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Seq);
                });

            migrationBuilder.CreateTable(
                name: "ChainHeads",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastHash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainHeads", x => new { x.TenantId, x.Category });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActorPseudonyms_Token",
                table: "ActorPseudonyms",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventId",
                table: "AuditEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_Action",
                table: "AuditEvents",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_ActorRef",
                table: "AuditEvents",
                columns: new[] { "TenantId", "ActorRef" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_Category_Seq",
                table: "AuditEvents",
                columns: new[] { "TenantId", "Category", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "TenantId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_TargetType_TargetId",
                table: "AuditEvents",
                columns: new[] { "TenantId", "TargetType", "TargetId" });

            // Append-only, enforced by the database and not only by this
            // service's code (docs/architecture/16-auditing-and-compliance.md
            // section 4). Two NOLOGIN group roles carry the privileges; the
            // service's actual login is made a member of audit_writer, and the
            // retention/erasure job's login a member of audit_purger, by
            // whoever provisions the database. Note that creating a role needs
            // CREATEROLE (or a superuser, as in the local stack), so this
            // migration must run as a suitably privileged owner.
            //
            // audit_writer can add events, read them back, and maintain the
            // chain heads - and can never UPDATE or DELETE an event, so a bug
            // or a compromise of the service itself cannot rewrite history.
            // audit_purger is the one exception: it may DELETE events past
            // their retention window and erase an actor's pseudonym.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'audit_writer') THEN
                        CREATE ROLE audit_writer NOLOGIN;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'audit_purger') THEN
                        CREATE ROLE audit_purger NOLOGIN;
                    END IF;
                END
                $$;

                GRANT SELECT, INSERT ON "AuditEvents" TO audit_writer;
                GRANT SELECT, INSERT ON "ActorPseudonyms" TO audit_writer;
                GRANT SELECT, INSERT, UPDATE ON "ChainHeads" TO audit_writer;

                GRANT SELECT, DELETE ON "AuditEvents" TO audit_purger;
                GRANT SELECT, DELETE ON "ActorPseudonyms" TO audit_purger;
                GRANT SELECT ON "ChainHeads" TO audit_purger;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The grants go with the tables. The two group roles are cluster
            // level and may be in use by other databases' provisioning, so they
            // are deliberately left in place.
            migrationBuilder.DropTable(
                name: "ActorPseudonyms");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "ChainHeads");
        }
    }
}

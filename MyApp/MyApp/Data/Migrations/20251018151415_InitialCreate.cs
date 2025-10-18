using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditTrailEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitHubOAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubOAuthStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserExternalLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExternalUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalLogins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_UserId_EventType_OccurredAt",
                table: "AuditTrailEntries",
                columns: new[] { "UserId", "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubOAuthStates_State",
                table: "GitHubOAuthStates",
                column: "State",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_UserId_Provider",
                table: "UserExternalLogins",
                columns: new[] { "UserId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditTrailEntries");

            migrationBuilder.DropTable(
                name: "GitHubOAuthStates");

            migrationBuilder.DropTable(
                name: "UserExternalLogins");
        }
    }
}

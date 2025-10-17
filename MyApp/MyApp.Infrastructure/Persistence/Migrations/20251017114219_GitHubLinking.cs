using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GitHubLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubAccountLinks",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GitHubAccountId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GitHubLogin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GitHubDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GitHubAvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SecretName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LinkedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRefreshed = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubAccountLinks", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserExternalLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    State = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SecretName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalLogins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_State",
                table: "UserExternalLogins",
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
                name: "GitHubAccountLinks");

            migrationBuilder.DropTable(
                name: "UserExternalLogins");
        }
    }
}

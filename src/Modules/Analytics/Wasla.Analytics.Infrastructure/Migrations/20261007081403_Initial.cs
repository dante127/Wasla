using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Analytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.CreateTable(
                name: "DailyRollups",
                schema: "analytics",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ConversationsOpened = table.Column<int>(type: "integer", nullable: false),
                    ConversationsResolved = table.Column<int>(type: "integer", nullable: false),
                    MessagesInbound = table.Column<int>(type: "integer", nullable: false),
                    MessagesOutbound = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRollups", x => new { x.TenantId, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "FactConversations",
                schema: "analytics",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstResponseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MessageCountInbound = table.Column<int>(type: "integer", nullable: false),
                    MessageCountOutbound = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactConversations", x => new { x.TenantId, x.ConversationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactConversations_TenantId_AssignedUserId",
                schema: "analytics",
                table: "FactConversations",
                columns: new[] { "TenantId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FactConversations_TenantId_ChannelId",
                schema: "analytics",
                table: "FactConversations",
                columns: new[] { "TenantId", "ChannelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRollups",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "FactConversations",
                schema: "analytics");
        }
    }
}

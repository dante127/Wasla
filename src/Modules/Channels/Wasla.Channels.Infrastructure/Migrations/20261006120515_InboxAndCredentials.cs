using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Channels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InboxAndCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHealthCheckAt",
                schema: "channels",
                table: "Channels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelCredentials",
                schema: "channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedValue = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxEvents",
                schema: "channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    BodyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HeadersFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelCredentials_ChannelId_Key",
                schema: "channels",
                table: "ChannelCredentials",
                columns: new[] { "ChannelId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxEvents_ChannelId_BodyHash",
                schema: "channels",
                table: "InboxEvents",
                columns: new[] { "ChannelId", "BodyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxEvents_Status_NextAttemptAt",
                schema: "channels",
                table: "InboxEvents",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelCredentials",
                schema: "channels");

            migrationBuilder.DropTable(
                name: "InboxEvents",
                schema: "channels");

            migrationBuilder.DropColumn(
                name: "LastHealthCheckAt",
                schema: "channels",
                table: "Channels");
        }
    }
}

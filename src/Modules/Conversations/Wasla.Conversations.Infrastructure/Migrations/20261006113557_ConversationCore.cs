using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Conversations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConversationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    FirstResponseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SlaDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SlaBreachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuickReplies",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationTags",
                schema: "conversations",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTags", x => new { x.ConversationId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ConversationTags_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationTimelineEntries",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Data = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTimelineEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationTimelineEntries_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InternalNotes",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalNotes_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteMentions",
                schema: "conversations",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteMentions", x => new { x.NoteId, x.MentionType, x.TargetId });
                    table.ForeignKey(
                        name: "FK_NoteMentions_InternalNotes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "conversations",
                        principalTable: "InternalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_AssignedTeamId",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "TenantId", "AssignedTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_AssignedUserId",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "TenantId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_CustomerId",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_Status_LastMessageAt",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "TenantId", "Status", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTags_TagId",
                schema: "conversations",
                table: "ConversationTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTimelineEntries_ConversationId_OccurredAt",
                schema: "conversations",
                table: "ConversationTimelineEntries",
                columns: new[] { "ConversationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalNotes_ConversationId",
                schema: "conversations",
                table: "InternalNotes",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplies_TenantId_Key",
                schema: "conversations",
                table: "QuickReplies",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationTags",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "ConversationTimelineEntries",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "NoteMentions",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "QuickReplies",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "InternalNotes",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "conversations");
        }
    }
}

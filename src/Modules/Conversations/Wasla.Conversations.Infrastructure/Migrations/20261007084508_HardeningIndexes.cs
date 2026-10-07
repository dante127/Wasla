using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Conversations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardeningIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_CustomerId_ChannelId",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "TenantId", "CustomerId", "ChannelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_TenantId_CustomerId_ChannelId",
                schema: "conversations",
                table: "Conversations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasla.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerActivities",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerActivities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerIdentities",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerIdentities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerNotes",
                schema: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTags",
                schema: "customers",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTags", x => new { x.CustomerId, x.TagId });
                    table.ForeignKey(
                        name: "FK_CustomerTags_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customers",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerActivities_CustomerId_OccurredAt",
                schema: "customers",
                table: "CustomerActivities",
                columns: new[] { "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId",
                schema: "customers",
                table: "CustomerContacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_TenantId_Type_Value",
                schema: "customers",
                table: "CustomerContacts",
                columns: new[] { "TenantId", "Type", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentities_CustomerId",
                schema: "customers",
                table: "CustomerIdentities",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentities_TenantId_ChannelType_ExternalId",
                schema: "customers",
                table: "CustomerIdentities",
                columns: new[] { "TenantId", "ChannelType", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotes_CustomerId",
                schema: "customers",
                table: "CustomerNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DisplayName",
                schema: "customers",
                table: "Customers",
                column: "DisplayName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Status",
                schema: "customers",
                table: "Customers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTags_TagId",
                schema: "customers",
                table: "CustomerTags",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerActivities",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerContacts",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerIdentities",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerNotes",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerTags",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "customers");
        }
    }
}

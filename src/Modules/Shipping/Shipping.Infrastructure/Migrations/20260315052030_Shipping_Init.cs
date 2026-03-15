using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shipping.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Shipping_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipping");

            migrationBuilder.CreateTable(
                name: "ShipmentBatches",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceRowCount = table.Column<int>(type: "integer", nullable: false),
                    LabelTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrintRequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentBatchItems",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    CustomerCode = table.Column<string>(type: "text", nullable: false),
                    PartNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PoItem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DueDate = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RunNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Store = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QrPayload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LabelCopies = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsPrinted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PrintedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ExclusionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentBatchItems_ShipmentBatches_ShipmentBatchId",
                        column: x => x.ShipmentBatchId,
                        principalSchema: "shipping",
                        principalTable: "ShipmentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentBatchRowErrors",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentBatchRowErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentBatchRowErrors_ShipmentBatches_ShipmentBatchId",
                        column: x => x.ShipmentBatchId,
                        principalSchema: "shipping",
                        principalTable: "ShipmentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatches_BatchNumber",
                schema: "shipping",
                table: "ShipmentBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatches_CreatedAtUtc",
                schema: "shipping",
                table: "ShipmentBatches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatches_PoReference",
                schema: "shipping",
                table: "ShipmentBatches",
                column: "PoReference");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatches_Status",
                schema: "shipping",
                table: "ShipmentBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatchItems_BatchId",
                schema: "shipping",
                table: "ShipmentBatchItems",
                column: "ShipmentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatchItems_BatchId_LineNumber",
                schema: "shipping",
                table: "ShipmentBatchItems",
                columns: new[] { "ShipmentBatchId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatchItems_PartNo",
                schema: "shipping",
                table: "ShipmentBatchItems",
                column: "PartNo");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatchRowErrors_BatchId",
                schema: "shipping",
                table: "ShipmentBatchRowErrors",
                column: "ShipmentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentBatchRowErrors_BatchId_Row",
                schema: "shipping",
                table: "ShipmentBatchRowErrors",
                columns: new[] { "ShipmentBatchId", "RowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentBatchItems",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "ShipmentBatchRowErrors",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "ShipmentBatches",
                schema: "shipping");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Printing_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "printing");

            migrationBuilder.CreateTable(
                name: "PrintRequests",
                schema: "printing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintRequestItems",
                schema: "printing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentBatchItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    PartNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PrintJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintRequestItems_PrintRequests_PrintRequestId",
                        column: x => x.PrintRequestId,
                        principalSchema: "printing",
                        principalTable: "PrintRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                schema: "printing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintRequestItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrinterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LabelTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelTemplateVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Copies = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FailCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QueuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_PrintRequestItems_PrintRequestItemId",
                        column: x => x.PrintRequestItemId,
                        principalSchema: "printing",
                        principalTable: "PrintRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrintResults",
                schema: "printing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintResults_PrintJobs_PrintJobId",
                        column: x => x.PrintJobId,
                        principalSchema: "printing",
                        principalTable: "PrintJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_CorrelationId",
                schema: "printing",
                table: "PrintJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_IdempotencyKey",
                schema: "printing",
                table: "PrintJobs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_PrinterId",
                schema: "printing",
                table: "PrintJobs",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_PrintRequestItemId",
                schema: "printing",
                table: "PrintJobs",
                column: "PrintRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Status",
                schema: "printing",
                table: "PrintJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Status_CreatedAtUtc",
                schema: "printing",
                table: "PrintJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_IdempotencyKey",
                schema: "printing",
                table: "PrintRequestItems",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_PrintRequestId",
                schema: "printing",
                table: "PrintRequestItems",
                column: "PrintRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_ShipmentBatchItemId",
                schema: "printing",
                table: "PrintRequestItems",
                column: "ShipmentBatchItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_Status",
                schema: "printing",
                table: "PrintRequestItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_BatchId",
                schema: "printing",
                table: "PrintRequests",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_IdempotencyKey",
                schema: "printing",
                table: "PrintRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_Status",
                schema: "printing",
                table: "PrintRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_Status_CreatedAtUtc",
                schema: "printing",
                table: "PrintRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintResults_IsSuccess",
                schema: "printing",
                table: "PrintResults",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "IX_PrintResults_PrintJobId",
                schema: "printing",
                table: "PrintResults",
                column: "PrintJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintResults",
                schema: "printing");

            migrationBuilder.DropTable(
                name: "PrintJobs",
                schema: "printing");

            migrationBuilder.DropTable(
                name: "PrintRequestItems",
                schema: "printing");

            migrationBuilder.DropTable(
                name: "PrintRequests",
                schema: "printing");
        }
    }
}

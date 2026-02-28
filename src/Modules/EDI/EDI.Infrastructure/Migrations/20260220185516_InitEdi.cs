using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EDI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitEdi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "edi");

            migrationBuilder.CreateTable(
                name: "EdiFileJobs",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ParsedRecords = table.Column<int>(type: "integer", nullable: false),
                    AppliedRecords = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiFileJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerProfiles",
                schema: "edi",
                columns: table => new
                {
                    PartnerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InboxPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProcessingPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ArchivePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ErrorPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerProfiles", x => x.PartnerCode);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderStagingHeaders",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransmissionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TransmissionTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    PoFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    SupplierCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderStagingHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Staging_ItemMaster",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RawLine = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staging_ItemMaster", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderStagingDetails",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PoNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PoItem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ItemNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BoiName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DueQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Um = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RawLine = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderStagingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderStagingDetails_PurchaseOrderStagingHeaders_Hea~",
                        column: x => x.HeaderId,
                        principalSchema: "edi",
                        principalTable: "PurchaseOrderStagingHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "edi",
                table: "PartnerProfiles",
                columns: new[] { "PartnerCode", "ArchivePath", "DisplayName", "ErrorPath", "Format", "InboxPath", "ProcessingPath", "SchemaVersion" },
                values: new object[] { "SAMPLE01", "/data/edi/SAMPLE01/archive", "Sample Partner 01", "/data/edi/SAMPLE01/error", "csv", "/data/edi/SAMPLE01/inbox", "/data/edi/SAMPLE01/processing", "1.0" });

            migrationBuilder.CreateIndex(
                name: "IX_EdiFileJobs_PartnerCode_Sha256",
                schema: "edi",
                table: "EdiFileJobs",
                columns: new[] { "PartnerCode", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdiFileJobs_ReceivedAtUtc",
                schema: "edi",
                table: "EdiFileJobs",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EdiFileJobs_Status",
                schema: "edi",
                table: "EdiFileJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderStagingDetails_HeaderId",
                schema: "edi",
                table: "PurchaseOrderStagingDetails",
                column: "HeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Staging_ItemMaster_JobId",
                schema: "edi",
                table: "Staging_ItemMaster",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdiFileJobs",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "PartnerProfiles",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "PurchaseOrderStagingDetails",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "Staging_ItemMaster",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "PurchaseOrderStagingHeaders",
                schema: "edi");
        }
    }
}

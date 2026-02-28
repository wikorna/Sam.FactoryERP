using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EDI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigDrivenEdi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileTypeCode",
                schema: "edi",
                table: "EdiFileJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EdiFileTypeConfigs",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileTypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FilenamePrefixPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Delimiter = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: ","),
                    HasHeaderRow = table.Column<bool>(type: "boolean", nullable: false),
                    HeaderLineCount = table.Column<int>(type: "integer", nullable: false),
                    SkipLines = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DetectionPriority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 52428800L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiFileTypeConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdiStagingRows",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileTypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    RawLine = table.Column<string>(type: "text", nullable: false),
                    ParsedColumnsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiStagingRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdiColumnDefinitions",
                schema: "edi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileTypeConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    ColumnName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "String"),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLength = table.Column<int>(type: "integer", nullable: true),
                    ValidationRegex = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdiColumnDefinitions_EdiFileTypeConfigs_FileTypeConfigId",
                        column: x => x.FileTypeConfigId,
                        principalSchema: "edi",
                        principalTable: "EdiFileTypeConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "edi",
                table: "EdiFileTypeConfigs",
                columns: new[] { "Id", "CreatedAtUtc", "Delimiter", "DetectionPriority", "DisplayName", "FileTypeCode", "FilenamePrefixPattern", "HasHeaderRow", "HeaderLineCount", "IsActive", "MaxFileSizeBytes", "SchemaVersion", "SkipLines", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), ",", 10, "SAP MCP Forecast", "SAP_FORECAST", "^F", true, 1, true, 52428800L, "1.0", 0, null },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), ",", 20, "SAP MCP Purchase Order", "SAP_PO", "^P", true, 1, true, 52428800L, "1.0", 0, null }
                });

            migrationBuilder.InsertData(
                schema: "edi",
                table: "EdiColumnDefinitions",
                columns: new[] { "Id", "ColumnName", "DataType", "DisplayLabel", "FileTypeConfigId", "IsRequired", "MaxLength", "Ordinal", "ValidationRegex" },
                values: new object[,]
                {
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000001"), "ForecastId", "String", "Forecast ID", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), true, 50, 0, null },
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000002"), "ItemCode", "String", "Item Code", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), true, 50, 1, null },
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000003"), "Description", "String", "Description", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), false, 255, 2, null },
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000004"), "Quantity", "Decimal", "Quantity", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), true, null, 3, null },
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000005"), "UoM", "String", "Unit", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), false, 20, 4, null },
                    { new Guid("b1b2c3d4-0001-0001-0001-000000000006"), "DueDate", "Date", "Due Date", new Guid("a1b2c3d4-0001-0001-0001-000000000001"), true, null, 5, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000001"), "PoNumber", "String", "PO Number", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), true, 50, 0, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000002"), "PoItem", "String", "PO Item", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), true, 10, 1, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000003"), "ItemCode", "String", "Item Code", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), true, 50, 2, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000004"), "Description", "String", "Description", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), false, 255, 3, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000005"), "Quantity", "Decimal", "Quantity", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), true, null, 4, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000006"), "UoM", "String", "Unit", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), false, 20, 5, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000007"), "UnitPrice", "Decimal", "Unit Price", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), false, null, 6, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000008"), "DueDate", "Date", "Due Date", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), true, null, 7, null },
                    { new Guid("b1b2c3d4-0002-0001-0001-000000000009"), "Currency", "String", "Currency", new Guid("a1b2c3d4-0001-0001-0001-000000000002"), false, 10, 8, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdiColumnDefinitions_FileTypeConfigId_Ordinal",
                schema: "edi",
                table: "EdiColumnDefinitions",
                columns: new[] { "FileTypeConfigId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdiFileTypeConfigs_FileTypeCode",
                schema: "edi",
                table: "EdiFileTypeConfigs",
                column: "FileTypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EdiFileTypeConfigs_IsActive",
                schema: "edi",
                table: "EdiFileTypeConfigs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EdiStagingRows_JobId",
                schema: "edi",
                table: "EdiStagingRows",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiStagingRows_JobId_IsSelected_IsValid",
                schema: "edi",
                table: "EdiStagingRows",
                columns: new[] { "JobId", "IsSelected", "IsValid" });

            migrationBuilder.CreateIndex(
                name: "IX_EdiStagingRows_JobId_RowIndex",
                schema: "edi",
                table: "EdiStagingRows",
                columns: new[] { "JobId", "RowIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdiColumnDefinitions",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "EdiStagingRows",
                schema: "edi");

            migrationBuilder.DropTable(
                name: "EdiFileTypeConfigs",
                schema: "edi");

            migrationBuilder.DropColumn(
                name: "FileTypeCode",
                schema: "edi",
                table: "EdiFileJobs");
        }
    }
}

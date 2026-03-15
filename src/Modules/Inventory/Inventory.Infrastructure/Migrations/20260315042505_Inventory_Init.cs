using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inventory_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LongDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MaterialGroup = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BaseUom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrossWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    NetWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemLocations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plant = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StorageLocation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemLocations_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inventory",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemUoms",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UomCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemUoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemUoms_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "inventory",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemLocations_ItemId_Plant_StorageLocation",
                schema: "inventory",
                table: "ItemLocations",
                columns: new[] { "ItemId", "Plant", "StorageLocation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemNumber",
                schema: "inventory",
                table: "Items",
                column: "ItemNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_MaterialGroup",
                schema: "inventory",
                table: "Items",
                column: "MaterialGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Status",
                schema: "inventory",
                table: "Items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUoms_ItemId_UomCode",
                schema: "inventory",
                table: "ItemUoms",
                columns: new[] { "ItemId", "UomCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemLocations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ItemUoms",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "inventory");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Notification_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    SourceEventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceModule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredRealtimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CorrelationId",
                schema: "notifications",
                table: "Notifications",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedUtc",
                schema: "notifications",
                table: "Notifications",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeduplicationKey",
                schema: "notifications",
                table: "Notifications",
                column: "DeduplicationKey",
                unique: true,
                filter: "\"DeduplicationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_NotificationId_UserId",
                schema: "notifications",
                table: "UserNotifications",
                columns: new[] { "NotificationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead_CreatedUtc",
                schema: "notifications",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserNotifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notifications");
        }
    }
}

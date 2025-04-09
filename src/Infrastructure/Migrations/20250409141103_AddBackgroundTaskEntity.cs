using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundTaskEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_tasks",
                schema: "appschema",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    user_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    task_type = table.Column<int>(type: "integer", nullable: false),
                    task_data = table.Column<string>(type: "jsonb", nullable: false),
                    result_data = table.Column<string>(type: "jsonb", nullable: false),
                    cancellation_requested = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_background_tasks_created_at",
                schema: "appschema",
                table: "background_tasks",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_background_tasks_status",
                schema: "appschema",
                table: "background_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_background_tasks_status_completed_at",
                schema: "appschema",
                table: "background_tasks",
                columns: new[] { "status", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_background_tasks_user_id",
                schema: "appschema",
                table: "background_tasks",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_tasks",
                schema: "appschema");
        }
    }
}

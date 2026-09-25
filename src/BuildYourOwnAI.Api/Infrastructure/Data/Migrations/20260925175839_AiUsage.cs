using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildYourOwnAI.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage", x => x.id);
                    table.CheckConstraint("ck_ai_usage_mode", "mode in ('ask', 'routing', 'study', 'upload', 'gap')");
                    table.CheckConstraint("ck_ai_usage_operation", "operation in ('chat', 'embedding', 'choice')");
                    table.ForeignKey(
                        name: "fk_ai_usage_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_occurred_at",
                table: "ai_usage",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_user_id_occurred_at",
                table: "ai_usage",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage");
        }
    }
}

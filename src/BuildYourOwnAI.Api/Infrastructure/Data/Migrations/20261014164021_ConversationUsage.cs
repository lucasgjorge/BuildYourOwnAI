using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildYourOwnAI.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConversationUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "conversation_id",
                table: "ai_usage",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_conversation_id_occurred_at",
                table: "ai_usage",
                columns: new[] { "conversation_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_usage_conversation_id_occurred_at",
                table: "ai_usage");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                table: "ai_usage");
        }
    }
}

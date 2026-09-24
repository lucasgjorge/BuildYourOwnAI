using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildYourOwnAI.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "study_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_study_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_sessions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "study_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    correct_option = table.Column<short>(type: "smallint", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    chosen_option = table.Column<short>(type: "smallint", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_study_questions", x => x.id);
                    table.CheckConstraint("ck_study_questions_chosen_option", "chosen_option is null or chosen_option between 0 and 3");
                    table.CheckConstraint("ck_study_questions_correct_option", "correct_option between 0 and 3");
                    table.CheckConstraint("ck_study_questions_options", "cardinality(options) = 4");
                    table.ForeignKey(
                        name: "fk_study_questions_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_study_questions_study_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "study_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_study_questions_document_id",
                table: "study_questions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_questions_session_id_position",
                table: "study_questions",
                columns: new[] { "session_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_study_sessions_organization_id",
                table: "study_sessions",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_questions");

            migrationBuilder.DropTable(
                name: "study_sessions");
        }
    }
}

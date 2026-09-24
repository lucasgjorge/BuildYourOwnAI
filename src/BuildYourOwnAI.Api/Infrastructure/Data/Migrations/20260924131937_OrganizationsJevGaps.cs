using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildYourOwnAI.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationsJevGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_documents_assistants_assistant_id",
                table: "documents");

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_organizations_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Door 1 backfill: one organization per existing assistant, reusing the assistant id as the organization id,
            // so documents.assistant_id already holds the organization id once renamed.
            migrationBuilder.Sql(
                "INSERT INTO organizations (id, owner_id, name, created_at) SELECT id, owner_id, name, created_at FROM assistants;");

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "assistants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE assistants SET organization_id = id;");

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "assistants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_assistants_asp_net_users_owner_id",
                table: "assistants");

            migrationBuilder.DropIndex(
                name: "ix_assistants_owner_id_created_at",
                table: "assistants");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "assistants");

            migrationBuilder.RenameColumn(
                name: "assistant_id",
                table: "documents",
                newName: "organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_documents_assistant_id_content_sha256",
                table: "documents",
                newName: "ix_documents_organization_id_content_sha256");

            migrationBuilder.AddColumn<string>(
                name: "routing_description",
                table: "assistants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gaps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assistant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question = table.Column<string>(type: "text", nullable: false),
                    normalized_question = table.Column<string>(type: "text", nullable: false),
                    ask_count = table.Column<int>(type: "integer", nullable: false),
                    first_asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gaps", x => x.id);
                    table.ForeignKey(
                        name: "fk_gaps_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_gaps_assistants_assistant_id",
                        column: x => x.assistant_id,
                        principalTable: "assistants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gaps_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gaps_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assistants_organization_id",
                table: "assistants",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_gaps_assistant_id",
                table: "gaps",
                column: "assistant_id");

            migrationBuilder.CreateIndex(
                name: "ix_gaps_document_id",
                table: "gaps",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_gaps_organization_id",
                table: "gaps",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_gaps_owner_id_organization_id_normalized_question",
                table: "gaps",
                columns: new[] { "owner_id", "organization_id", "normalized_question" },
                unique: true,
                filter: "status = 'open'")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_owner_id_created_at",
                table: "organizations",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_assistants_organizations_organization_id",
                table: "assistants",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_organizations_organization_id",
                table: "documents",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assistants_organizations_organization_id",
                table: "assistants");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_organizations_organization_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "gaps");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "ix_assistants_organization_id",
                table: "assistants");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "assistants");

            migrationBuilder.DropColumn(
                name: "routing_description",
                table: "assistants");

            migrationBuilder.RenameColumn(
                name: "organization_id",
                table: "documents",
                newName: "assistant_id");

            migrationBuilder.RenameIndex(
                name: "ix_documents_organization_id_content_sha256",
                table: "documents",
                newName: "ix_documents_assistant_id_content_sha256");

            migrationBuilder.AddColumn<string>(
                name: "owner_id",
                table: "assistants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_assistants_owner_id_created_at",
                table: "assistants",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_assistants_asp_net_users_owner_id",
                table: "assistants",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_assistants_assistant_id",
                table: "documents",
                column: "assistant_id",
                principalTable: "assistants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonAlias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Alias = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedAlias = table.Column<string>(type: "TEXT", nullable: true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonAlias_Person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonAlias_PersonId",
                table: "PersonAlias",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonAlias");
        }
    }
}

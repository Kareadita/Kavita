using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    public partial class YearlyStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesCount = table.Column<long>(type: "INTEGER", nullable: false),
                    VolumeCount = table.Column<long>(type: "INTEGER", nullable: false),
                    ChapterCount = table.Column<long>(type: "INTEGER", nullable: false),
                    FileCount = table.Column<long>(type: "INTEGER", nullable: false),
                    UserCount = table.Column<long>(type: "INTEGER", nullable: false),
                    GenreCount = table.Column<long>(type: "INTEGER", nullable: false),
                    PersonCount = table.Column<long>(type: "INTEGER", nullable: false),
                    TagCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerStatistics", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerStatistics");
        }
    }
}

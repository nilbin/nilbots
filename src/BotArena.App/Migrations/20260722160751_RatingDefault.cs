using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <inheritdoc />
    public partial class RatingDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "Bots" SET "Rating" = "Rating" + 1200 WHERE "Rating" < 600""");
            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "Bots",
                type: "double precision",
                nullable: false,
                defaultValue: 1200.0,
                oldClrType: typeof(double),
                oldType: "double precision");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "Bots",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldDefaultValue: 1200.0);
        }
    }
}

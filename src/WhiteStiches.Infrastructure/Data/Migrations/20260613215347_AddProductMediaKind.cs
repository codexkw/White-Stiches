using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiteStiches.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMediaKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaKind",
                table: "ProductImages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: the uploader has accepted .mp4 all along, so any media already stored with a
            // video extension is reclassified as Video (1) to render correctly. Everything else stays Image (0).
            migrationBuilder.Sql(
                "UPDATE [ProductImages] SET [MediaKind] = 1 " +
                "WHERE LOWER([Url]) LIKE '%.mp4' OR LOWER([Url]) LIKE '%.webm';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaKind",
                table: "ProductImages");
        }
    }
}

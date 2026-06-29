using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiteStiches.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionShowInMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowInMenu",
                table: "Collections",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowInMenu",
                table: "Collections");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhiteStiches.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EyebrowEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    EyebrowAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    TitleLine1En = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleLine1Ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleLine2En = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleLine2Ar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleLine2Italic = table.Column<bool>(type: "bit", nullable: false),
                    LedeEn = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    LedeAr = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    PrimaryCtaTextEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PrimaryCtaTextAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PrimaryCtaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SecondaryCtaTextEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SecondaryCtaTextAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SecondaryCtaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShowStats = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BannerImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BannerId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MediaKind = table.Column<int>(type: "int", nullable: false),
                    AltEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AltAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BannerImages_Banners_BannerId",
                        column: x => x.BannerId,
                        principalTable: "Banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BannerStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BannerId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LabelAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BannerStats_Banners_BannerId",
                        column: x => x.BannerId,
                        principalTable: "Banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannerImages_BannerId",
                table: "BannerImages",
                column: "BannerId");

            migrationBuilder.CreateIndex(
                name: "IX_BannerStats_BannerId",
                table: "BannerStats",
                column: "BannerId");

            // Seed one default hero banner mirroring the original hardcoded hero so the storefront
            // renders the admin-managed hero immediately. Done here (not in the startup DbSeeder) so it
            // runs exactly once under EF's migration lock — no race when Web + Admin start together.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Banners])
BEGIN
    DECLARE @now datetime2 = SYSUTCDATETIME();
    INSERT INTO [Banners]
        ([AdminLabel],[EyebrowEn],[EyebrowAr],[TitleLine1En],[TitleLine1Ar],[TitleLine2En],[TitleLine2Ar],
         [TitleLine2Italic],[LedeEn],[LedeAr],[PrimaryCtaTextEn],[PrimaryCtaTextAr],[PrimaryCtaUrl],
         [SecondaryCtaTextEn],[SecondaryCtaTextAr],[SecondaryCtaUrl],[IsActive],[ShowStats],[SortOrder],
         [CreatedAtUtc],[UpdatedAtUtc])
    VALUES
        (N'Spring · Summer 2026 (default)', N'Spring · Summer 2026', N'ربيع · صيف 2026',
         N'The art of', N'فن', N'quiet elegance.', N'الأناقة الهادئة.', 1,
         N'A curated edit of cuts, fabrics and silhouettes — designed for the woman who measures luxury in stitches, not labels.',
         N'تشكيلة مختارة من القصّات والأقمشة والسيلويتات — صُمِّمت للمرأة التي تقيس الفخامة بالغرزة لا بالعلامة.',
         N'Shop the Edit', N'تسوّقي التشكيلة', N'/collection',
         N'Watch the Film', N'شاهدي الفيلم', N'/collection', 1, 1, 0, @now, NULL);

    DECLARE @bid int = CAST(SCOPE_IDENTITY() AS int);

    INSERT INTO [BannerImages] ([BannerId],[Url],[MediaKind],[AltEn],[AltAr],[SortOrder],[CreatedAtUtc],[UpdatedAtUtc])
    VALUES
        (@bid, N'/assets/video/hero.mp4', 1, N'Spring 2026 editorial — Noor double-breasted dress', N'إطلالة ربيع 2026', 0, @now, NULL),
        (@bid, N'/assets/products/product-3.jpg', 0, N'Spring 2026 editorial — Noor double-breasted dress', N'إطلالة ربيع 2026', 1, @now, NULL);

    INSERT INTO [BannerStats] ([BannerId],[Value],[LabelEn],[LabelAr],[IsVisible],[SortOrder],[CreatedAtUtc],[UpdatedAtUtc])
    VALUES
        (@bid, N'01', N'Lookbook', N'لوك بوك', 1, 0, @now, NULL),
        (@bid, N'28', N'New Pieces', N'قطع جديدة', 1, 1, @now, NULL),
        (@bid, N'06', N'GCC Markets', N'أسواق الخليج', 1, 2, @now, NULL);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannerImages");

            migrationBuilder.DropTable(
                name: "BannerStats");

            migrationBuilder.DropTable(
                name: "Banners");
        }
    }
}

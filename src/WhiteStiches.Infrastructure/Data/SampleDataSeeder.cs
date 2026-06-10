using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Enums;

namespace WhiteStiches.Infrastructure.Data;

/// <summary>
/// Idempotent demo-catalog seeding so every dynamic storefront page has real data:
/// products (options/variants/images), one curated collection, journal content and
/// the demo discount codes. Opt-in via configuration["SeedSampleData"] == "true";
/// skipped entirely once any product exists.
/// Content is extracted from the static design reference in HTML/ (collection.html,
/// index.html, product.html, journal.html).
/// </summary>
public static class SampleDataSeeder
{
    private static readonly string[] Sizes = ["XS", "S", "M", "L", "XL"];

    private sealed record ImageSeed(string Url, string AltEn, string? ColorName = null);

    private sealed record ProductSeed(
        string TitleEn,
        string TitleAr,
        string Slug,
        string Type,
        string Tags,
        string CategorySlug,
        decimal Price,
        decimal? CompareAtPrice,
        bool IsFeatured,
        string[] Colors,
        ImageSeed[] Images,
        string DescriptionEn,
        string MaterialCareEn,
        string SizeFitEn);

    public static async Task SeedAsync(WhiteStichesDbContext db, ILogger logger)
    {
        if (await db.Products.AnyAsync())
        {
            logger.LogInformation("Sample data: products already exist — skipping demo catalog seeding.");
            return;
        }

        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug);

        // ---- 1 + 2. Products with images, options and size×colour variants ----
        var seeds = GetProductSeeds();
        var products = new List<Product>(seeds.Length);
        for (var i = 0; i < seeds.Length; i++)
        {
            products.Add(BuildProduct(seeds[i], i + 1, categories));
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        logger.LogInformation("Sample data: seeded {Products} products with {Variants} variants.",
            products.Count, products.Sum(p => p.Variants.Count));

        // ---- 3. Curated collection: The Spring Edit ----
        var springEdit = new Collection
        {
            TitleEn = "The Spring Edit",
            TitleAr = "إطلالة الربيع",
            Slug = "spring-edit",
            DescriptionEn = "Eight pieces our atelier keeps reaching for — fluid linens, soft tailoring and evening silk for the new season.",
            ImageUrl = "/assets/products/product-1.jpg",
            IsActive = true
        };

        string[] springSlugs =
        [
            "noor-double-breasted-dress",
            "sahara-safari-suit",
            "layla-evening-dress",
            "hala-cropped-jacket",
            "yasmeen-sheer-collar-top",
            "salma-linen-coord",
            "aisha-wrap-dress",
            "lina-silk-slip-dress"
        ];

        var productsBySlug = products.ToDictionary(p => p.Slug);
        for (var i = 0; i < springSlugs.Length; i++)
        {
            springEdit.CollectionProducts.Add(new CollectionProduct
            {
                Product = productsBySlug[springSlugs[i]],
                Position = i + 1
            });
        }

        db.Collections.Add(springEdit);
        await db.SaveChangesAsync();
        logger.LogInformation("Sample data: seeded collection \"{Collection}\" with {Count} products.",
            springEdit.TitleEn, springEdit.CollectionProducts.Count);

        // ---- 4. Journal categories + posts (from HTML/journal.html) ----
        var styling = new JournalCategory { NameEn = "Styling", NameAr = "تنسيق الإطلالات", Slug = "styling" };
        var atelier = new JournalCategory { NameEn = "Atelier", NameAr = "الأتيليه", Slug = "atelier" };
        var conversations = new JournalCategory { NameEn = "Conversations", NameAr = "حوارات", Slug = "conversations" };
        var behindTheSeams = new JournalCategory { NameEn = "Behind the Seams", NameAr = "خلف الدرزات", Slug = "behind-the-seams" };
        db.JournalCategories.AddRange(styling, atelier, conversations, behindTheSeams);
        await db.SaveChangesAsync();

        db.JournalPosts.AddRange(BuildJournalPosts(styling, atelier, conversations, behindTheSeams));
        await db.SaveChangesAsync();
        logger.LogInformation("Sample data: seeded 4 journal categories and 7 journal posts.");

        // ---- 5. Discount codes (match the demo codes in wwwroot/js/site.js) ----
        db.DiscountCodes.AddRange(
            new DiscountCode { Code = "WELCOME10", Type = DiscountType.FixedAmount, Value = 10.000m, IsActive = true },
            new DiscountCode { Code = "SS26", Type = DiscountType.FixedAmount, Value = 15.000m, IsActive = true },
            new DiscountCode { Code = "EID2026", Type = DiscountType.FixedAmount, Value = 20.000m, IsActive = true });
        await db.SaveChangesAsync();
        logger.LogInformation("Sample data: seeded 3 discount codes (WELCOME10, SS26, EID2026).");
    }

    private static Product BuildProduct(ProductSeed seed, int productIndex, IReadOnlyDictionary<string, Category> categories)
    {
        var product = new Product
        {
            TitleEn = seed.TitleEn,
            TitleAr = seed.TitleAr,
            Slug = seed.Slug,
            DescriptionEn = seed.DescriptionEn,
            MaterialCareEn = seed.MaterialCareEn,
            SizeFitEn = seed.SizeFitEn,
            Type = seed.Type,
            Vendor = "White Stitches",
            Tags = seed.Tags,
            Status = ProductStatus.Active,
            IsFeatured = seed.IsFeatured,
            CategoryId = categories.TryGetValue(seed.CategorySlug, out var category) ? category.Id : null
        };

        for (var i = 0; i < seed.Images.Length; i++)
        {
            product.Images.Add(new ProductImage
            {
                Url = seed.Images[i].Url,
                AltEn = seed.Images[i].AltEn,
                ColorName = seed.Images[i].ColorName,
                SortOrder = i
            });
        }

        product.Options.Add(new ProductOption
        {
            NameEn = "Size",
            NameAr = "المقاس",
            Position = 1,
            ValuesCsv = string.Join(",", Sizes)
        });
        product.Options.Add(new ProductOption
        {
            NameEn = "Colour",
            NameAr = "اللون",
            Position = 2,
            ValuesCsv = string.Join(",", seed.Colors)
        });

        var position = 0;
        for (var c = 0; c < seed.Colors.Length; c++)
        {
            foreach (var size in Sizes)
            {
                position++;
                product.Variants.Add(new ProductVariant
                {
                    Sku = $"WS-{productIndex:000}-{size}-{ColorCode(seed.Colors[c])}",
                    Option1 = size,
                    Option2 = seed.Colors[c],
                    Price = seed.Price,
                    CompareAtPrice = seed.CompareAtPrice,
                    StockQuantity = StockFor(productIndex, position, c, seed.Colors.Length, size),
                    IsActive = true,
                    Position = position
                });
            }
        }

        return product;
    }

    /// <summary>First three letters of the colour name, uppercased (e.g., "Oat" → "OAT", "Charcoal" → "CHA").</summary>
    private static string ColorCode(string color)
    {
        var letters = new string(color.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return letters.Length <= 3 ? letters : letters[..3];
    }

    /// <summary>Deterministic stock spread: mostly 8–25, a few 2–3 (low stock) and at least one 0 (sold out).</summary>
    private static int StockFor(int productIndex, int position, int colorIndex, int colorCount, string size)
    {
        var stock = 8 + ((productIndex * 5) + (position * 3)) % 18;

        if (productIndex % 3 == 0 && colorIndex == 0 && size == "XS")
        {
            stock = 2; // low stock
        }

        if (productIndex % 4 == 1 && colorIndex == colorCount - 1 && size == "L")
        {
            stock = 3; // low stock
        }

        if (productIndex % 5 == 1 && colorIndex == colorCount - 1 && size == "XL")
        {
            stock = 0; // sold out (matches the PDP demo's sold-out XL)
        }

        if (productIndex == 1 && colorIndex == 0 && size == "S")
        {
            stock = 6; // PDP demo: "Only 6 left in this size"
        }

        return stock;
    }

    private static ProductSeed[] GetProductSeeds() =>
    [
        // ---- Home featured grid (index.html) + collection.html card 1 ----
        new ProductSeed(
            "Noor Double-Breasted Dress", "فستان نور بصفّين من الأزرار", "noor-double-breasted-dress",
            "Dress", "dresses,atelier,wool-crepe,bestseller", "dresses",
            42.500m, null, IsFeatured: true,
            ["Oat", "Black"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Noor Double-Breasted Dress — front view", "Oat"),
                new ImageSeed("/assets/products/product-1.jpg", "Noor Double-Breasted Dress — back view", "Black"),
                new ImageSeed("/assets/products/product-2.jpg", "Noor Double-Breasted Dress — detail shot"),
                new ImageSeed("/assets/products/product-3.jpg", "Noor Double-Breasted Dress — fabric close-up")
            ],
            """
            <p>The Noor is a floor-length double-breasted dress crafted from a structured wool crepe, finished with hand-set crystal buttons running the full length of the bodice. Sheer organza collar and cuffs detailed with hand-applied flocked dots add a couture flourish without breaking the silhouette.</p>
            <p>Designed and finished at our Kuwait atelier. Each piece carries a single white stitch on the inside seam — our maker's signature.</p>
            """,
            """
            <ul>
                <li>62% European linen · 38% viscose</li>
                <li>Mother-of-pearl button at neckline</li>
                <li>OEKO-TEX Standard 100 certified</li>
                <li>Hand wash cold or dry-clean · iron on reverse · do not bleach</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 178 cm and wears size S</li>
                <li>True to size · order your usual</li>
                <li>Mid-calf length · approx. 118 cm from shoulder on size S</li>
                <li>Unlined · slightly relaxed through the bodice</li>
            </ul>
            """),

        new ProductSeed(
            "Sahara Safari Suit", "طقم سهارى سفاري", "sahara-safari-suit",
            "Suit", "suits,tailoring,cotton-linen,spring", "suits",
            68.000m, null, IsFeatured: true,
            ["Stone", "Black"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Sahara Safari Suit — front view", "Stone"),
                new ImageSeed("/assets/products/product-3.jpg", "Sahara Safari Suit — back view"),
                new ImageSeed("/assets/products/product-4.jpg", "Sahara Safari Suit — styling on model")
            ],
            """
            <p>A relaxed two-piece safari suit cut from a washed cotton-linen twill, with patch pockets and a self-tie belt that draws the jacket in at the waist. Wear it as a set for quiet authority, or split the pieces across a week of looks.</p>
            <p>Cut, pressed and finished by hand at our Kuwait atelier.</p>
            """,
            """
            <ul>
                <li>55% cotton · 45% European linen twill</li>
                <li>Corozo buttons · self-fabric belt</li>
                <li>Machine wash cold, gentle cycle · line dry · warm iron</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 176 cm and wears size S</li>
                <li>Relaxed through the shoulder · order your usual size</li>
                <li>Jacket hits at mid hip · trouser is full length with a 30 cm leg opening</li>
            </ul>
            """),

        new ProductSeed(
            "Layla Evening Dress", "فستان ليلى للسهرة", "layla-evening-dress",
            "Dress", "dresses,evening,sale,occasion", "dresses",
            76.000m, 95.000m, IsFeatured: true,
            ["Champagne", "Ink"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Layla Evening Dress — front view", "Champagne"),
                new ImageSeed("/assets/products/product-1.jpg", "Layla Evening Dress — back view"),
                new ImageSeed("/assets/products/product-3.jpg", "Layla Evening Dress — detail shot")
            ],
            """
            <p>An evening column in liquid satin-back crepe with a sheer champagne overlay that catches the light as you move. The portrait neckline and gently flared hem are cut for candlelit dinners and long goodbyes.</p>
            <p>Each gown is finished by a single seamstress, start to end, in our Kuwait atelier.</p>
            """,
            """
            <ul>
                <li>Main: 97% satin-back crepe · 3% elastane</li>
                <li>Overlay: 100% silk organza</li>
                <li>Dry-clean only · cool iron on reverse · store hung</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 179 cm and wears size S</li>
                <li>Fitted through the bodice · take your usual size</li>
                <li>Floor length · approx. 148 cm from shoulder on size S</li>
            </ul>
            """),

        new ProductSeed(
            "Hala Cropped Jacket", "جاكيت هلا القصير", "hala-cropped-jacket",
            "Jacket", "jackets,tailoring,cropped,spring", "jackets",
            38.500m, null, IsFeatured: true,
            ["Oat", "Olive"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Hala Cropped Jacket — front view", "Oat"),
                new ImageSeed("/assets/products/product-3.jpg", "Hala Cropped Jacket — back view"),
                new ImageSeed("/assets/products/product-1.jpg", "Hala Cropped Jacket — detail shot")
            ],
            """
            <p>A cropped, collarless jacket in brushed cotton gabardine that sits exactly at the waistband of a high-rise trouser. Hidden hooks keep the front clean; the sleeves are finished a half-inch short to show a cuff or a bracelet.</p>
            <p>The shoulder is softly padded by hand — structure without stiffness.</p>
            """,
            """
            <ul>
                <li>100% brushed cotton gabardine · cupro lining</li>
                <li>Concealed hook-and-bar closure</li>
                <li>Dry-clean recommended · cool iron with pressing cloth</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 177 cm and wears size S</li>
                <li>Cropped at the natural waist · true to size</li>
                <li>Slightly shortened bracelet-length sleeve</li>
            </ul>
            """),

        // ---- Remaining collection.html grid cards ----
        new ProductSeed(
            "Yasmeen Sheer-Collar Top", "بلوزة ياسمين بياقة شفافة", "yasmeen-sheer-collar-top",
            "Top", "tops,new,organza,workwear", "tops",
            34.000m, null, IsFeatured: false,
            ["White", "Sand"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Yasmeen Sheer-Collar Top — front view", "White"),
                new ImageSeed("/assets/products/product-1.jpg", "Yasmeen Sheer-Collar Top — back view")
            ],
            """
            <p>A poplin shirt reimagined with a sheer organza collar and covered buttons. The body is cut generous, the cuffs deep enough to fold back twice.</p>
            <p>Tucks into tailoring as cleanly as it falls over a slip skirt.</p>
            """,
            """
            <ul>
                <li>Body: 100% cotton poplin · Collar: silk organza</li>
                <li>Covered corozo buttons</li>
                <li>Hand wash cold · drip dry · cool iron, avoid the collar</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 175 cm and wears size S</li>
                <li>Generous through the body · size down for a closer fit</li>
                <li>Hits at high hip · deep fold-back cuffs</li>
            </ul>
            """),

        new ProductSeed(
            "Reem Pearl-Button Top", "بلوزة ريم بأزرار لؤلؤية", "reem-pearl-button-top",
            "Top", "tops,knitwear,evening,pearl", "tops",
            28.500m, null, IsFeatured: false,
            ["Gold", "Silver"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Reem Pearl-Button Top — front view", "Gold"),
                new ImageSeed("/assets/products/product-3.jpg", "Reem Pearl-Button Top — back view")
            ],
            """
            <p>A fine-gauge knitted top scattered with hand-sewn pearl buttons along the shoulder seam. The neckline is wide enough to slip off one shoulder when the evening calls for it.</p>
            <p>Each pearl is knotted individually so a lost button never becomes a lost row.</p>
            """,
            """
            <ul>
                <li>70% viscose · 30% silk fine-gauge knit</li>
                <li>Freshwater pearl buttons, hand-knotted</li>
                <li>Hand wash cold in a mesh bag · dry flat</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 176 cm and wears size S</li>
                <li>Close fit with natural stretch · true to size</li>
                <li>Hits at the waistband</li>
            </ul>
            """),

        new ProductSeed(
            "Dima Three-Piece Suit", "طقم ديما من ثلاث قطع", "dima-three-piece-suit",
            "Suit", "suits,tailoring,three-piece,workwear", "suits",
            22.000m, null, IsFeatured: false,
            ["Bone", "Olive"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Dima Three-Piece Suit — front view", "Bone"),
                new ImageSeed("/assets/products/product-1.jpg", "Dima Three-Piece Suit — back view")
            ],
            """
            <p>Waistcoat, blazer and trouser in one decisive look, cut from a breathable viscose-blend suiting. Each piece is sized to be worn alone — together they read formal; apart, effortless.</p>
            <p>An entry point to our tailoring, priced for the first suit you actually live in.</p>
            """,
            """
            <ul>
                <li>68% viscose · 28% polyester · 4% elastane suiting</li>
                <li>Half-canvas blazer construction</li>
                <li>Dry-clean only · steam to refresh between wears</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 177 cm and wears size S</li>
                <li>True to size across all three pieces</li>
                <li>Trouser is high-rise with a straight, full-length leg</li>
            </ul>
            """),

        new ProductSeed(
            "Mariam Tuxedo Dress", "فستان مريم توكسيدو", "mariam-tuxedo-dress",
            "Dress", "dresses,evening,tuxedo,occasion", "dresses",
            54.000m, null, IsFeatured: false,
            ["Tan", "Black"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Mariam Tuxedo Dress — front view", "Tan"),
                new ImageSeed("/assets/products/product-3.jpg", "Mariam Tuxedo Dress — back view")
            ],
            """
            <p>A tuxedo dress with satin lapels and a single covered button at the waist. The hem falls just below the knee, finished with a discreet back vent for movement.</p>
            <p>Borrowed-from-the-boys tailoring, redrawn for an evening of her own.</p>
            """,
            """
            <ul>
                <li>Main: stretch wool blend · Lapels: duchess satin</li>
                <li>Fully lined in cupro</li>
                <li>Dry-clean only · press lapels through a cloth</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 178 cm and wears size S</li>
                <li>Tailored fit · take your usual size</li>
                <li>Hem falls just below the knee · back vent</li>
            </ul>
            """),

        new ProductSeed(
            "Salma Linen Co-ord", "طقم سلمى الكتاني", "salma-linen-coord",
            "Co-ord", "suits,new,linen,resort", "suits",
            42.000m, null, IsFeatured: false,
            ["Stone", "Black"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Salma Linen Co-ord — front view", "Stone"),
                new ImageSeed("/assets/products/product-1.jpg", "Salma Linen Co-ord — back view")
            ],
            """
            <p>A boxy camp-collar shirt and wide trouser in midweight European linen, garment-washed for softness from the first wear. The co-ord that carries you from a Friday brunch to a beach-house evening.</p>
            <p>Creases are part of the charm — but a warm iron restores it instantly.</p>
            """,
            """
            <ul>
                <li>100% European linen, garment-washed</li>
                <li>Natural shell buttons</li>
                <li>Machine wash cold · line dry · warm iron while damp</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 175 cm and wears size S</li>
                <li>Boxy, relaxed fit · size down for a neater line</li>
                <li>Trouser is high-rise with a wide, full-length leg</li>
            </ul>
            """),

        new ProductSeed(
            "Latifa Sequin Jacket", "جاكيت لطيفة بالترتر", "latifa-sequin-jacket",
            "Jacket", "jackets,evening,sequin,occasion", "jackets",
            118.000m, null, IsFeatured: false,
            ["Camel", "Charcoal"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Latifa Sequin Jacket — front view", "Camel"),
                new ImageSeed("/assets/products/product-3.jpg", "Latifa Sequin Jacket — back view")
            ],
            """
            <p>Tonal matte sequins arranged by hand on a collarless cocktail jacket — shine without noise. Fully lined in cupro silk so it slides over anything.</p>
            <p>Over 60 hours of hand-application go into every piece; no two catch the light the same way.</p>
            """,
            """
            <ul>
                <li>Base: silk-blend crepe · matte resin sequins, hand-applied</li>
                <li>Cupro silk lining</li>
                <li>Specialist dry-clean only · store flat in the provided cloth bag</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 177 cm and wears size S</li>
                <li>True to size · straight through the body</li>
                <li>Hits at mid hip · bracelet-length sleeve</li>
            </ul>
            """),

        new ProductSeed(
            "Aisha Wrap Dress", "فستان عائشة بقصة لف", "aisha-wrap-dress",
            "Dress", "dresses,sale,wrap,everyday", "dresses",
            44.500m, 55.000m, IsFeatured: false,
            ["Ivory", "Black"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Aisha Wrap Dress — front view", "Ivory"),
                new ImageSeed("/assets/products/product-1.jpg", "Aisha Wrap Dress — back view"),
                new ImageSeed("/assets/products/product-3.jpg", "Aisha Wrap Dress — detail shot")
            ],
            """
            <p>A true wrap dress in crepe de chine that ties at the side and skims everything in between. The angled hem lengthens the line of the leg without a heel.</p>
            <p>The wrap is cut deep enough to stay put — no hidden snaps required.</p>
            """,
            """
            <ul>
                <li>100% crepe de chine</li>
                <li>Self-fabric wrap ties</li>
                <li>Hand wash cold or dry-clean · cool iron on reverse</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 176 cm and wears size S</li>
                <li>Adjustable wrap fit · true to size</li>
                <li>Midi length with an angled hem</li>
            </ul>
            """),

        new ProductSeed(
            "Farah Tailored Jacket", "جاكيت فرح المفصّل", "farah-tailored-jacket",
            "Jacket", "jackets,tailoring,wool,workwear", "jackets",
            48.000m, null, IsFeatured: false,
            ["Oat", "Forest"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Farah Tailored Jacket — front view", "Oat"),
                new ImageSeed("/assets/products/product-3.jpg", "Farah Tailored Jacket — back view")
            ],
            """
            <p>A single-breasted jacket with a softly padded shoulder and a nipped waist, cut from a stretch wool blend. The interior is finished as carefully as the exterior — French seams throughout.</p>
            <p>The jacket our pattern cutter wears to work, most days.</p>
            """,
            """
            <ul>
                <li>96% wool · 4% elastane</li>
                <li>Horn buttons · French-seamed interior</li>
                <li>Dry-clean only · steam between wears</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 178 cm and wears size S</li>
                <li>Tailored through the waist · true to size</li>
                <li>Hits at low hip · full-length sleeve</li>
            </ul>
            """),

        // ---- "Complete the look" / "Recently viewed" pieces (product.html) ----
        new ProductSeed(
            "Atelier Tailored Blazer", "بليزر الأتيليه المفصّل", "atelier-tailored-blazer",
            "Blazer", "jackets,tailoring,longline,workwear", "jackets",
            68.000m, null, IsFeatured: false,
            ["Oat", "Black"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Atelier Tailored Blazer — front view", "Oat"),
                new ImageSeed("/assets/products/product-1.jpg", "Atelier Tailored Blazer — back view")
            ],
            """
            <p>Our house blazer: a longline single-button silhouette in compact twill with a structured shoulder and a slightly extended lapel. Cut to layer over silk or stand alone over denim.</p>
            <p>The pattern hasn't changed since our first season — only the cloth does.</p>
            """,
            """
            <ul>
                <li>74% wool · 24% viscose · 2% elastane compact twill</li>
                <li>Half-canvas construction · horn button</li>
                <li>Dry-clean only · hang on a shaped hanger</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 179 cm and wears size S</li>
                <li>Longline · true to size, size up to layer over knitwear</li>
                <li>Hits at upper thigh</li>
            </ul>
            """),

        new ProductSeed(
            "Sahar Wide-Leg Trouser", "بنطال سحر الواسع", "sahar-wide-leg-trouser",
            "Trouser", "suits,tailoring,wide-leg,everyday", "suits",
            38.500m, null, IsFeatured: false,
            ["Stone", "Ink"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Sahar Wide-Leg Trouser — front view", "Stone"),
                new ImageSeed("/assets/products/product-3.jpg", "Sahar Wide-Leg Trouser — back view")
            ],
            """
            <p>A high-rise, wide-leg trouser with a flat front and a floor-skimming hem, cut in a fluid suiting that never creases on the school run or the red-eye. Pairs with everything in this edit.</p>
            <p>The waistband is curved, not straight — it sits without gaping.</p>
            """,
            """
            <ul>
                <li>64% viscose · 32% polyester · 4% elastane fluid suiting</li>
                <li>Curved waistband · concealed side zip</li>
                <li>Machine wash cold, gentle cycle · hang to dry · cool iron</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 176 cm and wears size S</li>
                <li>High-rise · true to size at the waist</li>
                <li>Full length, cut to skim the floor with a low heel</li>
            </ul>
            """),

        new ProductSeed(
            "Lina Silk Slip Dress", "فستان لينا الحريري", "lina-silk-slip-dress",
            "Dress", "dresses,sale,silk,evening", "dresses",
            76.000m, 95.000m, IsFeatured: false,
            ["Champagne", "Black"],
            [
                new ImageSeed("/assets/products/product-4.jpg", "Lina Silk Slip Dress — front view", "Champagne"),
                new ImageSeed("/assets/products/product-1.jpg", "Lina Silk Slip Dress — back view"),
                new ImageSeed("/assets/products/product-2.jpg", "Lina Silk Slip Dress — detail shot")
            ],
            """
            <p>A bias-cut slip dress in sandwashed silk charmeuse with adjustable hand-finished straps. It moves like water and packs to nothing — the most borrowed piece in our studio.</p>
            <p>Wear it alone in the evening, or under the Atelier blazer by day.</p>
            """,
            """
            <ul>
                <li>100% sandwashed silk charmeuse</li>
                <li>Hand-finished adjustable straps</li>
                <li>Hand wash cold with silk detergent · dry flat in shade · cool iron on reverse</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 178 cm and wears size S</li>
                <li>Bias cut skims the body · between sizes, size up</li>
                <li>Midi length · adjustable straps fine-tune the fall</li>
            </ul>
            """),

        new ProductSeed(
            "Editorial Cotton Shirt", "قميص قطني إديتوريال", "editorial-cotton-shirt",
            "Shirt", "tops,cotton,oversized,everyday", "tops",
            34.000m, null, IsFeatured: false,
            ["White", "Sand"],
            [
                new ImageSeed("/assets/products/product-2.jpg", "Editorial Cotton Shirt — front view", "White"),
                new ImageSeed("/assets/products/product-3.jpg", "Editorial Cotton Shirt — back view")
            ],
            """
            <p>A crisp organic-cotton shirt with an editorial oversize collar and a curved, longer-at-the-back hem. The kind of white shirt you replace only with the same one.</p>
            <p>Single-needle stitching throughout — 22 stitches to the inch.</p>
            """,
            """
            <ul>
                <li>100% organic cotton poplin</li>
                <li>Single-needle construction · shell buttons</li>
                <li>Machine wash warm · tumble low · iron while damp for best finish</li>
            </ul>
            """,
            """
            <ul>
                <li>Model is 177 cm and wears size S</li>
                <li>Oversized fit · take your usual size for the intended look</li>
                <li>Curved hem, longer at the back</li>
            </ul>
            """)
    ];

    private static JournalPost[] BuildJournalPosts(
        JournalCategory styling,
        JournalCategory atelier,
        JournalCategory conversations,
        JournalCategory behindTheSeams) =>
    [
        new JournalPost
        {
            Slug = "the-mira-draped-midi-three-ways",
            TitleEn = "The Mira Draped Midi, three ways.",
            TitleAr = "فستان ميرا الميدي بثلاث إطلالات",
            ExcerptEn = "From day to dinner — styling notes for the piece we can't stop wearing.",
            Category = styling,
            Tags = "styling,mira,featured",
            AuthorName = "Layla Al-Sabah",
            HeroImageUrl = "/assets/products/product-4.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 4,
            BodyEn = """
                <p>Some pieces earn a place in the rotation; the Mira simply refuses to leave it. Since the first sample came off the rail in January, it has been borrowed, photographed, returned and borrowed again by nearly everyone on the team. The draping at the hip does most of the work — which means styling it is less about adding and more about deciding how little you can get away with.</p>
                <p>For day, we flatten everything around it: a white poplin shirt knotted loosely at the waist, flat leather sandals, a canvas tote. The Mira's neckline sits wide enough to take a collar underneath without bunching, and the midi length means you can walk the Avenues end to end without thinking about it once.</p>
                <blockquote>A dress should solve the morning, not add to it. The Mira was drafted around that single sentence.</blockquote>
                <p>For the office, swap the sandals for a pointed slingback and add the Atelier Tailored Blazer left open. The drape reads as intentional next to sharp tailoring — softness against structure is the whole story, so resist the urge to belt it.</p>
                <p>And for dinner, remove everything except jewellery. A crescent earring, a thin gold cuff, a heeled mule. Let the fabric catch the restaurant light and do what it was cut to do. Three ways, one dress, no repeats noticed.</p>
                """
        },
        new JournalPost
        {
            Slug = "a-morning-in-our-sharq-workshop",
            TitleEn = "A morning in our Sharq workshop.",
            TitleAr = "صباح في ورشتنا بشرق",
            ExcerptEn = "Where every piece begins — with tea, a kettle, and 14 pairs of hands.",
            Category = atelier,
            Tags = "atelier,workshop,kuwait",
            AuthorName = "Dana Al-Mutairi",
            HeroImageUrl = "/assets/products/product-1.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 28, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 6,
            BodyEn = """
                <p>The kettle goes on at seven. By the time the first cups of karak are poured, the cutting table has been brushed down, the day's cloth is resting flat, and the light through the east windows of our Sharq workshop is doing exactly what we built the room for. Nothing is cut before eight; linen, like people, behaves better once it has settled into the temperature of the room.</p>
                <p>Fourteen people work here. Some have been with us since the first season, when the "workshop" was two tables and a borrowed steam press. Each garment moves between stations — cutting, first seams, pressing, finishing — but it is never anonymous: a card travels with every piece, signed at each stage by the hands it passed through.</p>
                <blockquote>Machines are honest, but hands are accountable. That is why every White Stitches piece carries a signature seam.</blockquote>
                <p>The last station is the quietest. One maker, one chair, one spool of white thread. The single white stitch sewn into the inside seam of every piece takes less than a minute and is the entire point of the company — proof that a person, not a process, decided the garment was finished.</p>
                <p>By two in the afternoon the racks are full, the kettle is on again, and tomorrow's cloth is already resting. If you are ever in Sharq, knock. We will pour you a cup and show you the table where your dress began.</p>
                """
        },
        new JournalPost
        {
            Slug = "in-conversation-with-our-pattern-cutter-reem",
            TitleEn = "In conversation with our pattern cutter, Reem.",
            TitleAr = "حوار مع قصّاصة الباترون ريم",
            ExcerptEn = "After 18 years of drafting patterns by hand, Reem still works from intuition more than measurement.",
            Category = conversations,
            Tags = "conversations,pattern-cutting,craft",
            AuthorName = "Noura Al-Rashid",
            HeroImageUrl = "/assets/products/product-2.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 21, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 8,
            BodyEn = """
                <p>Reem's table is the oldest thing in the workshop — a slab of beech she brought with her eighteen years ago and refuses to replace. On it: a ruler she rarely touches, a stick of tailor's chalk worn to a thumbnail, and a roll of pattern paper that she reads the way other people read a newspaper.</p>
                <p>"Measurements tell you where the body is," she says, drafting the curve of a sleeve head freehand while we talk. "They never tell you where the body is going. A woman raises her arm to call a waiter, to hold a child, to wave across a wedding hall. The pattern has to already know that."</p>
                <blockquote>I can teach the maths of a sleeve in a week. The listening takes the eighteen years.</blockquote>
                <p>She drafted the Noor's double-breasted bodice eleven times before she let it near a sewing machine. The eleventh version differs from the fourth by three millimetres at the shoulder seam. When we suggest no customer would ever notice, she looks genuinely offended. "Noticing is my job, not theirs. If they could see it, I would have failed."</p>
                <p>Before we leave, she shows us the drawer where every rejected draft is kept, dated and folded. "The mistakes are the education," she shrugs. "You keep your diplomas. I keep mine."</p>
                """
        },
        new JournalPost
        {
            Slug = "building-a-capsule-for-the-ramadan-season",
            TitleEn = "Building a capsule for the Ramadan season.",
            TitleAr = "تكوين تشكيلة كبسولة لموسم رمضان",
            ExcerptEn = "Six pieces that work as a sehoor kaftan as easily as an iftar dinner outfit.",
            Category = styling,
            Tags = "styling,capsule,ramadan",
            AuthorName = "Layla Al-Sabah",
            HeroImageUrl = "/assets/products/product-3.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 5,
            BodyEn = """
                <p>Ramadan dressing has its own rhythm: long evenings, generous tables, rooms full of family, and a dress code that asks for ease and occasion at once. The mistake is buying for single nights. The better approach is a small capsule — six pieces that rotate through the whole month without repeating an exact look twice.</p>
                <p>Start with two anchors: a fluid maxi in a quiet tone and a wide trouser that pairs with everything. Add a sheer-collar top for the nights that lean formal, a linen co-ord for the daytime gatherings, and one piece with real shine — sequins or silk — held in reserve for the last ten nights.</p>
                <blockquote>The best Ramadan wardrobe is the one you never have to think about after maghrib.</blockquote>
                <p>Fabric matters more than usual this month. You will sit longer, eat later and move between air conditioning and courtyard heat a dozen times a night — washed linen, crepe de chine and unlined tailoring forgive all of it. Anything stiff will be abandoned by the second week.</p>
                <p>Finally, decide your jewellery once and stop. One pair of earrings, one cuff, worn every night. The repetition is not a limitation; it is a signature — and it makes the month's photographs look like a collection rather than a scramble.</p>
                """
        },
        new JournalPost
        {
            Slug = "why-we-make-our-earrings-by-hand",
            TitleEn = "Why we make our earrings by hand.",
            TitleAr = "لماذا نصنع أقراطنا يدويًا",
            ExcerptEn = "Our jewellery comes from a small studio in Old Kuwait — the goldsmith works on commission only.",
            Category = behindTheSeams,
            Tags = "behind-the-seams,jewellery,craft",
            AuthorName = "Sara Al-Enezi",
            HeroImageUrl = "/assets/products/product-4.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 4,
            BodyEn = """
                <p>The studio has no sign. You find it by the sound — a small, persistent tapping that has come from the same doorway in Old Kuwait for longer than anyone on our team has been alive. Inside, Abu Faisal works on commission only, which means our crescent earrings exist because he agreed they should, not because we ordered them.</p>
                <p>Each pair starts as a strip of recycled gold, annealed and curved by eye against a form his father made. There are no two identical pairs and there never will be; the tolerance of a hand-raised curve is the entire character of the piece. Machine perfection was available to us from day one. It was never interesting.</p>
                <blockquote>A machine repeats. A hand decides, every single time. You can feel the difference on the ear, even if you cannot name it.</blockquote>
                <p>It would be faster — outrageously faster — to cast them. A casting house quoted us four hundred pairs a week; Abu Faisal manages nine. We said yes to nine, and the waiting list is now part of the product. Things made at the speed of care arrive when they arrive.</p>
                """
        },
        new JournalPost
        {
            Slug = "dressing-for-kuwaits-45-summer",
            TitleEn = "Dressing for Kuwait's 45° summer.",
            TitleAr = "أناقة الصيف الكويتي في ٤٥ درجة",
            ExcerptEn = "The fabrics, cuts and tricks our team relies on to look pulled-together when it's unbearable outside.",
            Category = styling,
            Tags = "styling,summer,linen",
            AuthorName = "Dana Al-Mutairi",
            HeroImageUrl = "/assets/products/product-1.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 6, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 6,
            BodyEn = """
                <p>There is a particular week in June when the car-park thermometer stops being funny. Dressing well through a Kuwaiti summer is not a styling challenge; it is an engineering problem, and the engineering happens at the fibre level. Everything else is decoration.</p>
                <p>The rules our team actually follows: natural fibres only against the skin; nothing fully fitted between shoulder and hip; light colours outside, with the saturated tones saved for indoors where they can be admired in air conditioning. Garment-washed linen and crepe de chine carry the month better than anything else we cut.</p>
                <blockquote>Air must move through a garment, or the garment is working against you. Cut for the breeze you wish existed.</blockquote>
                <p>Silhouette does the rest. A wide trouser creates its own shade and its own draught; a boxy shirt keeps cloth off the spine; an angled hem means fabric touches you in fewer places. This is why our summer pieces look relaxed — the ease is load-bearing.</p>
                <p>And the one trick nobody believes until they try it: long sleeves, in the right linen, are cooler than bare arms at midday. The bedouin knew. The fashion industry forgot. We just cut what the climate already proved.</p>
                """
        },
        new JournalPost
        {
            Slug = "sourcing-fabrics-a-year-in-como",
            TitleEn = "Sourcing fabrics: a year in Como.",
            TitleAr = "رحلة الأقمشة: عام في كومو",
            ExcerptEn = "A visit to our Italian silk mill — and what we learned about getting fabrics right for our climate.",
            Category = behindTheSeams,
            Tags = "behind-the-seams,fabric,silk,como",
            AuthorName = "Noura Al-Rashid",
            HeroImageUrl = "/assets/products/product-2.jpg",
            IsPublished = true,
            PublishAtUtc = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc),
            ReadingTimeMinutes = 7,
            BodyEn = """
                <p>The mill sits above Lake Como behind a row of cypresses, third generation, forty looms, and a sampling room that smells faintly of raw silk and espresso. We arrived with a problem they had never been asked to solve: silk that would be worn at 45 degrees, moved repeatedly between desert heat and aggressive air conditioning, and washed at home by busy women who do not own a steamer.</p>
                <p>The first samples failed beautifully. Gorgeous hand, perfect drape — and a charmeuse that water-spotted if you looked at it with humid intent. Over four visits and eleven months, the mill re-twisted the yarn, adjusted the weight by a few grams per square metre, and developed the sandwashed finish that now defines our Lina slip dress.</p>
                <blockquote>You don't buy fabric from a list, the old weaver told us. You argue with it until it agrees to live where your customer lives.</blockquote>
                <p>What we learned changed how we source everything: a fibre is only as good as its behaviour in the worst week of your climate, not the best. Every cloth we buy now passes a "July test" — a week of wear trials in Kuwait at the height of summer before it earns a place in the line.</p>
                <p>The relationship matters as much as the cloth. The mill keeps our specifications on file the way Reem keeps her rejected patterns — as the education that made the final answer possible. Next year, we start the same argument about wool.</p>
                """
        }
    ];
}

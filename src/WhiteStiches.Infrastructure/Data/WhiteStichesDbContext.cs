using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhiteStiches.Core.Entities;
using WhiteStiches.Core.Entities.Catalog;
using WhiteStiches.Core.Entities.Content;
using WhiteStiches.Core.Entities.Customers;
using WhiteStiches.Core.Entities.Marketing;
using WhiteStiches.Core.Entities.Orders;
using WhiteStiches.Core.Entities.Settings;
using WhiteStiches.Core.Entities.ShoppingCart;
using WhiteStiches.Infrastructure.Identity;

namespace WhiteStiches.Infrastructure.Data;

public class WhiteStichesDbContext(DbContextOptions<WhiteStichesDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionProduct> CollectionProducts => Set<CollectionProduct>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();

    // Customers
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    // Cart
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    // Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    // Marketing
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();

    // Content
    public DbSet<StaticPage> StaticPages => Set<StaticPage>();
    public DbSet<JournalCategory> JournalCategories => Set<JournalCategory>();
    public DbSet<JournalPost> JournalPosts => Set<JournalPost>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<BannerImage> BannerImages => Set<BannerImage>();
    public DbSet<BannerStat> BannerStats => Set<BannerStat>();

    // Settings & audit
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // KWD uses three decimals (LOC-03)
        configurationBuilder.Properties<decimal>().HavePrecision(18, 3);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Catalog ----
        builder.Entity<Category>(e =>
        {
            e.Property(x => x.NameEn).HasMaxLength(200);
            e.Property(x => x.NameAr).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(e =>
        {
            e.Property(x => x.TitleEn).HasMaxLength(300);
            e.Property(x => x.TitleAr).HasMaxLength(300);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.Vendor).HasMaxLength(150);
            e.Property(x => x.Tags).HasMaxLength(1000);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductImage>(e =>
        {
            e.Property(x => x.Url).HasMaxLength(1000);
            e.Property(x => x.ColorName).HasMaxLength(100);
            e.HasOne(x => x.Product)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductOption>(e =>
        {
            e.Property(x => x.NameEn).HasMaxLength(100);
            e.Property(x => x.NameAr).HasMaxLength(100);
            e.HasOne(x => x.Product)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductVariant>(e =>
        {
            e.Property(x => x.Sku).HasMaxLength(100);
            e.Property(x => x.Barcode).HasMaxLength(100);
            e.Property(x => x.Option1).HasMaxLength(100);
            e.Property(x => x.Option2).HasMaxLength(100);
            e.Property(x => x.Option3).HasMaxLength(100);
            e.HasIndex(x => x.Sku).IsUnique().HasFilter("[Sku] IS NOT NULL");
            e.HasOne(x => x.Product)
                .WithMany(x => x.Variants)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            // No referential action: variant and image both cascade from Product;
            // a second cascade path would be rejected by SQL Server.
            e.HasOne(x => x.Image)
                .WithMany()
                .HasForeignKey(x => x.ImageId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        builder.Entity<Collection>(e =>
        {
            e.Property(x => x.TitleEn).HasMaxLength(300);
            e.Property(x => x.TitleAr).HasMaxLength(300);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<CollectionProduct>(e =>
        {
            e.HasKey(x => new { x.CollectionId, x.ProductId });
            e.HasOne(x => x.Collection)
                .WithMany(x => x.CollectionProducts)
                .HasForeignKey(x => x.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
                .WithMany(x => x.CollectionProducts)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Banner>(e =>
        {
            e.Property(x => x.AdminLabel).HasMaxLength(160);
            e.Property(x => x.EyebrowEn).HasMaxLength(160);
            e.Property(x => x.EyebrowAr).HasMaxLength(160);
            e.Property(x => x.TitleLine1En).HasMaxLength(200);
            e.Property(x => x.TitleLine1Ar).HasMaxLength(200);
            e.Property(x => x.TitleLine2En).HasMaxLength(200);
            e.Property(x => x.TitleLine2Ar).HasMaxLength(200);
            e.Property(x => x.LedeEn).HasMaxLength(600);
            e.Property(x => x.LedeAr).HasMaxLength(600);
            e.Property(x => x.PrimaryCtaTextEn).HasMaxLength(80);
            e.Property(x => x.PrimaryCtaTextAr).HasMaxLength(80);
            e.Property(x => x.PrimaryCtaUrl).HasMaxLength(500);
            e.Property(x => x.SecondaryCtaTextEn).HasMaxLength(80);
            e.Property(x => x.SecondaryCtaTextAr).HasMaxLength(80);
            e.Property(x => x.SecondaryCtaUrl).HasMaxLength(500);
        });

        builder.Entity<BannerImage>(e =>
        {
            e.Property(x => x.Url).HasMaxLength(1000);
            e.HasOne(x => x.Banner)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.BannerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BannerStat>(e =>
        {
            e.Property(x => x.Value).HasMaxLength(20);
            e.Property(x => x.LabelEn).HasMaxLength(80);
            e.Property(x => x.LabelAr).HasMaxLength(80);
            e.HasOne(x => x.Banner)
                .WithMany(x => x.Stats)
                .HasForeignKey(x => x.BannerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InventoryAdjustment>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.ProductVariant)
                .WithMany(x => x.InventoryAdjustments)
                .HasForeignKey(x => x.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Customers ----
        builder.Entity<Address>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(100);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Country).HasMaxLength(2);
            e.Property(x => x.Governorate).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(150);
            e.Property(x => x.Block).HasMaxLength(50);
            e.Property(x => x.Street).HasMaxLength(200);
            e.Property(x => x.Building).HasMaxLength(100);
            e.Property(x => x.Floor).HasMaxLength(50);
            e.Property(x => x.Apartment).HasMaxLength(50);
            e.Property(x => x.Directions).HasMaxLength(500);
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<WishlistItem>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Cart ----
        builder.Entity<Cart>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.DiscountCode)
                .WithMany()
                .HasForeignKey(x => x.DiscountCodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CartItem>(e =>
        {
            e.HasOne(x => x.Cart)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProductVariant)
                .WithMany()
                .HasForeignKey(x => x.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CartId, x.ProductVariantId }).IsUnique();
        });

        // ---- Orders ----
        builder.Entity<Order>(e =>
        {
            e.Property(x => x.OrderNumber).HasMaxLength(30);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PaymentStatus);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.DiscountCodeSnapshot).HasMaxLength(100);
            e.Property(x => x.ShippingMethodName).HasMaxLength(100);
            e.Property(x => x.ShipFirstName).HasMaxLength(100);
            e.Property(x => x.ShipLastName).HasMaxLength(100);
            e.Property(x => x.ShipPhone).HasMaxLength(30);
            e.Property(x => x.ShipCountry).HasMaxLength(2);
            e.Property(x => x.ShipGovernorate).HasMaxLength(100);
            e.Property(x => x.ShipArea).HasMaxLength(150);
            e.Property(x => x.ShipBlock).HasMaxLength(50);
            e.Property(x => x.ShipStreet).HasMaxLength(200);
            e.Property(x => x.ShipBuilding).HasMaxLength(100);
            e.Property(x => x.ShipFloor).HasMaxLength(50);
            e.Property(x => x.ShipApartment).HasMaxLength(50);
            e.Property(x => x.ShipDirections).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.TitleEn).HasMaxLength(300);
            e.Property(x => x.TitleAr).HasMaxLength(300);
            e.Property(x => x.VariantDescription).HasMaxLength(200);
            e.Property(x => x.Sku).HasMaxLength(100);
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            // ProductId / ProductVariantId are intentionally plain snapshot columns (no FK):
            // order history must survive catalog deletions.
        });

        builder.Entity<OrderEvent>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.HasOne(x => x.Order)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Provider).HasMaxLength(50);
            e.Property(x => x.Method).HasMaxLength(50);
            e.Property(x => x.GatewayTransactionId).HasMaxLength(200);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasIndex(x => x.GatewayTransactionId);
            e.HasOne(x => x.Order)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Refund>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.GatewayRefundId).HasMaxLength(200);
            e.HasOne(x => x.Order)
                .WithMany(x => x.Refunds)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Payment)
                .WithMany()
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Shipment>(e =>
        {
            e.Property(x => x.Carrier).HasMaxLength(100);
            e.Property(x => x.AwbNumber).HasMaxLength(100);
            e.Property(x => x.TrackingUrl).HasMaxLength(1000);
            e.HasOne(x => x.Order)
                .WithMany(x => x.Shipments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReturnRequest>(e =>
        {
            e.Property(x => x.RmaNumber).HasMaxLength(30);
            e.HasIndex(x => x.RmaNumber).IsUnique();
            e.Property(x => x.CustomerReason).HasMaxLength(1000);
            e.Property(x => x.Method).HasMaxLength(30);
            e.Property(x => x.StaffNote).HasMaxLength(1000);
            e.HasOne(x => x.Order)
                .WithMany(x => x.ReturnRequests)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReturnItem>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Condition).HasMaxLength(500);
            e.HasOne(x => x.ReturnRequest)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            // NoAction: OrderItem already cascades from Order; second path would cycle.
            e.HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ---- Marketing ----
        builder.Entity<DiscountCode>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(100);
            e.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<NewsletterSubscriber>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Source).HasMaxLength(50);
        });

        // ---- Content ----
        builder.Entity<StaticPage>(e =>
        {
            e.Property(x => x.Slug).HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.TitleEn).HasMaxLength(300);
            e.Property(x => x.TitleAr).HasMaxLength(300);
        });

        builder.Entity<JournalCategory>(e =>
        {
            e.Property(x => x.NameEn).HasMaxLength(150);
            e.Property(x => x.NameAr).HasMaxLength(150);
            e.Property(x => x.Slug).HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<JournalPost>(e =>
        {
            e.Property(x => x.Slug).HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.TitleEn).HasMaxLength(300);
            e.Property(x => x.TitleAr).HasMaxLength(300);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.Property(x => x.HeroImageUrl).HasMaxLength(1000);
            e.Property(x => x.Tags).HasMaxLength(1000);
            e.HasOne(x => x.Category)
                .WithMany(x => x.Posts)
                .HasForeignKey(x => x.JournalCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContactMessage>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.Body).HasMaxLength(4000);
        });

        // ---- Settings & audit ----
        builder.Entity<StoreSetting>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(200);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Group).HasMaxLength(100);
        });

        builder.Entity<AuditLogEntry>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(200);
            e.Property(x => x.EntityType).HasMaxLength(200);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => x.Action);
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

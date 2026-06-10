namespace WhiteStiches.Infrastructure.Identity;

/// <summary>Role names per PRD Section 9 — User Roles &amp; Permissions.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string OperationsManager = "OperationsManager";
    public const string MarketingManager = "MarketingManager";
    public const string ContentEditor = "ContentEditor";
    public const string CustomerService = "CustomerService";
    public const string InventoryStaff = "InventoryStaff";
    public const string ReadOnlyAuditor = "ReadOnlyAuditor";
    public const string Customer = "Customer";

    /// <summary>Every staff role — used for the Admin panel's base authorization policy.</summary>
    public static readonly string[] StaffRoles =
    [
        SuperAdmin, Admin, OperationsManager, MarketingManager,
        ContentEditor, CustomerService, InventoryStaff, ReadOnlyAuditor
    ];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [SuperAdmin] = "Unrestricted; only role managing other admins, integration secrets, and theme code",
        [Admin] = "Everything except Super Admin management, code editor, and destructive actions",
        [OperationsManager] = "Orders, fulfilment, inventory, customers, returns, reports",
        [MarketingManager] = "Discounts, campaigns, content, analytics; read-only orders",
        [ContentEditor] = "Pages, journal, navigation, theme content; no orders or finances",
        [CustomerService] = "Orders (read, limited refunds), customer profiles, conversations, draft orders",
        [InventoryStaff] = "Products and inventory adjustments; no pricing or financial visibility",
        [ReadOnlyAuditor] = "View access across modules; no edits, no PII export",
        [Customer] = "Self-service: shop, account, wishlist, orders, returns"
    };
}

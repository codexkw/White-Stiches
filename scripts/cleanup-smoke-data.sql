/* =============================================================================
   White Stitches — remove smoke/test data
   -----------------------------------------------------------------------------
   Deletes the rows created by scripts/smoke-e2e.ps1 and scripts/smoke-admin.ps1.
   Every filter is tightly scoped to the smoke naming patterns
   (slug 'smoke-cat-/smoke-coll-/smoke-page-/smoke-journal-',
    title 'Smoke Product ', email '...@test.whitestiches.kw' /
    'smoke-staff-...@whitestiches.kw') so it cannot match real catalog/customers.

   Safe to re-run (idempotent). Run against DB: White-Stiches.

   TIER 1 + TIER 2 below are FK-clean and auto-COMMIT.
   TIER 3 (customers + their orders) is OPTIONAL and left commented — uncomment
   only if you also want to purge the smoke customer/staff accounts.
   ============================================================================= */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- TIER 1: catalog + content (this is what clears the storefront nav) ----------
   FK behavior (from the EF model):
     Products      -> Images/Options/Variants/InventoryAdjustments/CollectionProducts/WishlistItems = CASCADE;
                      OrderItems are snapshots with NO product FK, so real orders are untouched.
     Collections   -> CollectionProducts = CASCADE.
     Categories    -> Product.CategoryId = SET NULL (real products just lose the dead category link).
*/
DELETE FROM Products     WHERE TitleEn LIKE 'Smoke Product %';
PRINT 'Products deleted      : ' + CAST(@@ROWCOUNT AS varchar(10));

DELETE FROM Collections  WHERE Slug LIKE 'smoke-coll-%';
PRINT 'Collections deleted   : ' + CAST(@@ROWCOUNT AS varchar(10));

DELETE FROM Categories   WHERE Slug LIKE 'smoke-cat-%';
PRINT 'Categories deleted    : ' + CAST(@@ROWCOUNT AS varchar(10));

DELETE FROM StaticPages  WHERE Slug LIKE 'smoke-page-%';
PRINT 'StaticPages deleted   : ' + CAST(@@ROWCOUNT AS varchar(10));

DELETE FROM JournalPosts WHERE Slug LIKE 'smoke-journal-%';
PRINT 'JournalPosts deleted  : ' + CAST(@@ROWCOUNT AS varchar(10));

/* ---------- TIER 2: marketing / inbox ---------- */
DELETE FROM NewsletterSubscribers
  WHERE Email LIKE 'news%@test.whitestiches.kw' OR Email LIKE 'smoke%@test.whitestiches.kw';
PRINT 'Newsletter deleted    : ' + CAST(@@ROWCOUNT AS varchar(10));

DELETE FROM ContactMessages
  WHERE Email LIKE 'smoke%@test.whitestiches.kw' OR Name = 'Smoke Tester';
PRINT 'ContactMessages del.  : ' + CAST(@@ROWCOUNT AS varchar(10));

COMMIT;
PRINT '== TIER 1 + 2 committed ==';

/* ---------- TIER 3 (OPTIONAL): smoke customers + staff and their data ----------
   The domain tables reference users by a loose UserId Guid with NO DB foreign key,
   so child rows must be removed explicitly. Review, then uncomment to run.

BEGIN TRAN;
DECLARE @users TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @users
  SELECT Id FROM AspNetUsers
  WHERE Email LIKE 'smoke%@test.whitestiches.kw'
     OR Email LIKE 'smoke-staff-%@whitestiches.kw';

DELETE FROM Orders        WHERE UserId IN (SELECT Id FROM @users);  -- cascades OrderItems/Events/Payments/Refunds/Shipments/Returns
PRINT 'Orders deleted        : ' + CAST(@@ROWCOUNT AS varchar(10));
DELETE FROM Carts         WHERE UserId IN (SELECT Id FROM @users);  -- cascades CartItems
DELETE FROM WishlistItems WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM Addresses     WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM AspNetUserRoles  WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM AspNetUserClaims WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM AspNetUserLogins WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM AspNetUserTokens WHERE UserId IN (SELECT Id FROM @users);
DELETE FROM AspNetUsers      WHERE Id     IN (SELECT Id FROM @users);
PRINT 'Users deleted         : ' + CAST(@@ROWCOUNT AS varchar(10));
COMMIT;
PRINT '== TIER 3 committed ==';
*/

# ============================================================================
# smoke-admin.ps1 — scripted back-office journey against a running Admin app.
# Covers: login -> dashboard -> products (create/options/variants/inventory) ->
# categories -> collections -> discounts -> orders+drafts -> returns -> customers
# -> content (pages/journal/inbox) -> settings -> staff -> audit -> logout.
# Usage: run the Admin app first, then:
#   .\scripts\smoke-admin.ps1 [-BaseUrl http://localhost:5200]
# ============================================================================
param(
    [string]$BaseUrl = 'http://localhost:5200',
    [string]$AdminEmail = 'admin@whitestiches.kw',
    [string]$AdminPassword = 'ChangeMe@WS-2026!'
)

$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$script:failures = @()

function Step([string]$name, [scriptblock]$body) {
    try { & $body; Write-Host ("PASS  {0}" -f $name) }
    catch { $script:failures += $name; Write-Host ("FAIL  {0}  -> {1}" -f $name, $_.Exception.Message) }
}

function Get-Page([string]$path) {
    Invoke-WebRequest -Uri "$BaseUrl$path" -WebSession $session -UseBasicParsing -TimeoutSec 30
}

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw 'no antiforgery token on page' }
    $m.Groups[1].Value
}

# GET $tokenPath to mint a token, then POST $fields to $path (same session).
function Post-Form([string]$path, [hashtable]$fields, [string]$tokenPath) {
    $page = Get-Page $tokenPath
    $fields['__RequestVerificationToken'] = Get-Token $page.Content
    Invoke-WebRequest -Uri "$BaseUrl$path" -Method Post -Body $fields -WebSession $session -UseBasicParsing -TimeoutSec 30
}

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$script:newProductId = $null
$script:firstVariantId = $null
$script:collectionId = $null
$script:draftId = $null
$script:draftOrderNumber = $null

Step 'login as admin' {
    $r = Post-Form '/login' @{ Email = $AdminEmail; Password = $AdminPassword } '/login'
    if ($r.BaseResponse.ResponseUri.AbsolutePath -like '*/login*') { throw 'still on login page after sign-in' }
}

Step 'dashboard renders' {
    $r = Get-Page '/'
    if ($r.BaseResponse.ResponseUri.AbsolutePath -like '*/login*') { throw 'redirected to login — not authenticated' }
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

# ---------------------------------------------------------------- Categories
Step 'create category' {
    Post-Form '/categories/save' @{
        Id = '0'; NameEn = "Smoke Cat $stamp"; NameAr = ''; Slug = "smoke-cat-$stamp"
        SortOrder = '0'; IsActive = 'true'
    } '/categories' | Out-Null
    $check = Get-Page '/categories'
    if ($check.Content -notmatch [regex]::Escape("smoke-cat-$stamp")) { throw 'new category not listed' }
}

# ---------------------------------------------------------------- Products
Step 'create product (lands on edit)' {
    $r = Post-Form '/products/save' @{
        Id = '0'; TitleEn = "Smoke Product $stamp"; TitleAr = ''; Status = 'Active'; IsFeatured = 'false'
    } '/products/new'
    $m = [regex]::Match($r.BaseResponse.ResponseUri.AbsolutePath, '/products/(\d+)/edit')
    if (-not $m.Success) { throw "did not redirect to product edit (was $($r.BaseResponse.ResponseUri.AbsolutePath))" }
    $script:newProductId = $m.Groups[1].Value
}

Step 'set options (regenerates variant matrix)' {
    Post-Form "/products/$script:newProductId/options" @{
        name1 = 'Size'; values1 = 'S,M'; name2 = ''; values2 = ''; name3 = ''; values3 = ''
    } "/products/$script:newProductId/edit" | Out-Null
    $edit = Get-Page "/products/$script:newProductId/edit"
    $ids = [regex]::Matches($edit.Content, 'name="variants\[(\d+)\]\.Id"[^>]*value="(\d+)"')
    if ($ids.Count -lt 2) { throw "expected >=2 variants after options, found $($ids.Count)" }
    $script:firstVariantId = $ids[0].Groups[2].Value
}

Step 'save variant prices + stock' {
    $edit = Get-Page "/products/$script:newProductId/edit"
    $ids = [regex]::Matches($edit.Content, 'name="variants\[(\d+)\]\.Id"[^>]*value="(\d+)"')
    $fields = @{}
    foreach ($mm in $ids) {
        $i = $mm.Groups[1].Value
        $fields["variants[$i].Id"] = $mm.Groups[2].Value
        $fields["variants[$i].Price"] = '12.500'
        $fields["variants[$i].StockQuantity"] = '20'
        $fields["variants[$i].LowStockThreshold"] = '5'
        $fields["variants[$i].IsActive"] = 'true'
    }
    Post-Form "/products/$script:newProductId/variants" $fields "/products/$script:newProductId/edit" | Out-Null
    $after = Get-Page "/products/$script:newProductId/edit"
    if ($after.Content -notmatch '12\.500') { throw 'variant price not persisted' }
}

Step 'adjust inventory +5' {
    Post-Form "/products/$script:newProductId/inventory" @{
        variantId = $script:firstVariantId; delta = '5'; reason = 'Received'; note = "smoke $stamp"
    } "/products/$script:newProductId/inventory" | Out-Null
    $inv = Get-Page "/products/$script:newProductId/inventory"
    if ($inv.Content -notmatch 'Received') { throw 'adjustment not shown in history' }
}

Step 'product appears in list search' {
    $r = Get-Page "/products?search=Smoke+Product+$stamp"
    if ($r.Content -notmatch [regex]::Escape("Smoke Product $stamp")) { throw 'product not found via search' }
}

# ---------------------------------------------------------------- Collections
Step 'create manual collection' {
    $r = Post-Form '/collections/save' @{
        Id = '0'; TitleEn = "Smoke Collection $stamp"; TitleAr = ''; Slug = "smoke-coll-$stamp"
        SortOrder = 'Manual'; IsActive = 'true'; IsSmart = 'false'
    } '/collections/new'
    $m = [regex]::Match($r.BaseResponse.ResponseUri.AbsolutePath, '/collections/(\d+)')
    if ($m.Success) { $script:collectionId = $m.Groups[1].Value }
    else {
        $list = Get-Page '/collections'
        $lm = [regex]::Match($list.Content, '/collections/(\d+)/edit')
        if (-not $lm.Success) { throw 'collection not created' }
        $script:collectionId = $lm.Groups[1].Value
    }
}

Step 'add product to collection' {
    Post-Form "/collections/$script:collectionId/products/add" @{
        productId = $script:newProductId
    } "/collections/$script:collectionId/edit" | Out-Null
    $edit = Get-Page "/collections/$script:collectionId/edit"
    if ($edit.Content -notmatch [regex]::Escape("Smoke Product $stamp")) { throw 'product not added to collection' }
}

# ---------------------------------------------------------------- Discounts
Step 'create discount code' {
    Post-Form '/discounts/save' @{
        Id = '0'; Code = "SMOKE$stamp"; Type = 'FixedAmount'; Value = '5.000'
        IsActive = 'true'
    } '/discounts/new' | Out-Null
    $r = Get-Page '/discounts?search=SMOKE'
    if ($r.Content -notmatch [regex]::Escape("SMOKE$stamp")) { throw 'discount code not listed' }
}

Step 'newsletter list renders' {
    $r = Get-Page '/newsletter'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

# ---------------------------------------------------------------- Orders
Step 'orders list renders' {
    $r = Get-Page '/orders'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'open newest order detail (if any)' {
    $list = Get-Page '/orders'
    $m = [regex]::Match($list.Content, '/orders/(\d+)"')
    if (-not $m.Success) { Write-Host '   (skip: no orders yet)'; return }
    $oid = $m.Groups[1].Value
    $detail = Get-Page "/orders/$oid"
    if ($detail.StatusCode -ne 200) { throw "order detail status $($detail.StatusCode)" }
    Post-Form "/orders/$oid/comment" @{ text = "Smoke note $stamp" } "/orders/$oid" | Out-Null
    $after = Get-Page "/orders/$oid"
    if ($after.Content -notmatch [regex]::Escape("Smoke note $stamp")) { throw 'staff comment not on timeline' }
}

Step 'create draft order' {
    $r = Post-Form '/orders/drafts/create' @{
        email = "draft$stamp@test.kw"; phone = '+96550000000'; firstName = 'Draft'; lastName = 'Buyer'
        channel = 'Instagram'
    } '/orders/drafts/new'
    $m = [regex]::Match($r.BaseResponse.ResponseUri.AbsolutePath, '/orders/drafts/(\d+)')
    if (-not $m.Success) { throw "did not land on draft editor (was $($r.BaseResponse.ResponseUri.AbsolutePath))" }
    $script:draftId = $m.Groups[1].Value
}

Step 'add item to draft + convert' {
    Post-Form "/orders/drafts/$script:draftId/items/add" @{
        variantId = $script:firstVariantId; quantity = '1'
    } "/orders/drafts/$script:draftId" | Out-Null
    $draft = Get-Page "/orders/drafts/$script:draftId"
    if ($draft.Content -notmatch [regex]::Escape("Smoke Product $stamp")) { throw 'item not added to draft' }
    $conv = Post-Form "/orders/drafts/$script:draftId/convert" @{} "/orders/drafts/$script:draftId"
    $list = Get-Page '/orders'
    if ($list.StatusCode -ne 200) { throw "orders list status $($list.StatusCode) after convert" }
}

# ---------------------------------------------------------------- Returns / Customers
Step 'returns queue renders' {
    $r = Get-Page '/returns'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'customers list renders' {
    $r = Get-Page '/customers?q=smoke'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

# ---------------------------------------------------------------- Content
Step 'create static page' {
    Post-Form '/pages/save' @{
        Id = '0'; TitleEn = "Smoke Page $stamp"; TitleAr = ''; Slug = "smoke-page-$stamp"
        BodyEn = '<p>Smoke body</p>'; IsPublished = 'true'
    } '/pages/new' | Out-Null
    $r = Get-Page '/pages'
    if ($r.Content -notmatch [regex]::Escape("smoke-page-$stamp")) { throw 'page not listed' }
}

Step 'create journal post' {
    Post-Form '/journal/save' @{
        Id = '0'; TitleEn = "Smoke Journal $stamp"; TitleAr = ''; Slug = "smoke-journal-$stamp"
        ExcerptEn = 'Excerpt'; BodyEn = '<p>Body</p>'; AuthorName = 'Smoke'; IsPublished = 'true'
    } '/journal/new' | Out-Null
    $r = Get-Page '/journal'
    if ($r.Content -notmatch [regex]::Escape("Smoke Journal $stamp")) { throw 'journal post not listed' }
}

Step 'inbox renders' {
    $r = Get-Page '/inbox?unreadOnly=false'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

# ---------------------------------------------------------------- Settings
Step 'settings save (store group, no-op safe)' {
    $page = Get-Page '/settings'
    $cur = [regex]::Match($page.Content, 'name="store\.name\.en"[^>]*value="([^"]*)"')
    $val = if ($cur.Success) { $cur.Groups[1].Value } else { 'White Stitches' }
    $r = Post-Form '/settings/save?group=store' @{ 'store.name.en' = $val } '/settings'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

# ---------------------------------------------------------------- Staff
Step 'create staff member' {
    Post-Form '/staff/create' @{
        firstName = 'Smoke'; lastName = "Staff$stamp"; email = "smoke-staff-$stamp@whitestiches.kw"
        password = 'Smoke@Staff1234'; roles = 'ContentEditor'
    } '/staff/new' | Out-Null
    $r = Get-Page '/staff'
    if ($r.Content -notmatch [regex]::Escape("smoke-staff-$stamp@whitestiches.kw")) { throw 'staff member not listed' }
}

# ---------------------------------------------------------------- Audit
Step 'audit log captured this run' {
    $r = Get-Page '/audit'
    if ($r.Content -notmatch 'product\.create' -and $r.Content -notmatch 'category\.create' -and $r.Content -notmatch 'staff\.create') {
        throw 'no expected audit actions found'
    }
}

# ---------------------------------------------------------------- Logout
Step 'logout' {
    Post-Form '/logout' @{} '/' | Out-Null
    $after = Invoke-WebRequest -Uri "$BaseUrl/orders" -WebSession $session -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($after.StatusCode -eq 200) { throw 'still authenticated after logout' }
}

Write-Host ''
if ($script:failures.Count -eq 0) {
    Write-Host 'ADMIN SMOKE: ALL PASS'
} else {
    Write-Host ("ADMIN SMOKE: {0} FAILURE(S): {1}" -f $script:failures.Count, ($script:failures -join '; '))
    exit 1
}

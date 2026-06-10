# ============================================================================
# smoke-e2e.ps1 — scripted customer journey against a running storefront.
# Covers: browse → PDP → cart → discount → register → checkout → order in
# account → addresses → wishlist → contact → newsletter → track → logout.
# Usage: run the Web app first, then:  .\scripts\smoke-e2e.ps1 [-BaseUrl http://localhost:5100]
# ============================================================================
param([string]$BaseUrl = 'http://localhost:5100')

$ErrorActionPreference = 'Stop'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$script:failures = @()

function Step([string]$name, [scriptblock]$body) {
    try {
        & $body
        Write-Host ("PASS  {0}" -f $name)
    } catch {
        $script:failures += $name
        Write-Host ("FAIL  {0}  -> {1}" -f $name, $_.Exception.Message)
    }
}

function Get-Page([string]$path) {
    Invoke-WebRequest -Uri "$BaseUrl$path" -WebSession $session -UseBasicParsing -TimeoutSec 30
}

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw 'no antiforgery token on page' }
    $m.Groups[1].Value
}

function Post-Form([string]$path, [hashtable]$fields, [string]$tokenPath) {
    $page = Get-Page $tokenPath
    $fields['__RequestVerificationToken'] = Get-Token $page.Content
    Invoke-WebRequest -Uri "$BaseUrl$path" -Method Post -Body $fields -WebSession $session -UseBasicParsing -TimeoutSec 30
}

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$email = "smoke$stamp@test.whitestiches.kw"
$password = 'Smoke@Test1234'
$script:productSlug = $null
$script:productId = $null
$script:variantId = $null
$script:orderNumber = $null

Step 'home renders' {
    $r = Get-Page '/'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'collection lists seeded products' {
    $r = Get-Page '/collection'
    $m = [regex]::Match($r.Content, 'href="/products/([a-z0-9-]+)"')
    if (-not $m.Success) { throw 'no product links found' }
    $script:productSlug = $m.Groups[1].Value
}

Step 'collection filter by category works' {
    $r = Get-Page '/collection?category=dresses'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'PDP renders with variant data' {
    $r = Get-Page "/products/$script:productSlug"
    $v = [regex]::Match($r.Content, 'name="variantId"[^>]*value="(\d+)"')
    if (-not $v.Success) { $v = [regex]::Match($r.Content, 'value="(\d+)"[^>]*name="variantId"') }
    if (-not $v.Success) { throw 'no variantId input on PDP' }
    $script:variantId = $v.Groups[1].Value
    $p = [regex]::Match($r.Content, 'name="productId"[^>]*value="(\d+)"')
    if ($p.Success) { $script:productId = $p.Groups[1].Value }
}

Step 'add to cart' {
    $r = Post-Form '/cart/items' @{ variantId = $script:variantId; quantity = '1'; returnUrl = '/cart' } "/products/$script:productSlug"
    if ($r.Content -notmatch 'cart') { throw 'cart page not reached' }
}

Step 'cart shows item' {
    $r = Get-Page '/cart'
    if ($r.Content -match 'id="miniCartEmpty"' -and $r.Content -notmatch 'qty') { throw 'cart appears empty' }
}

Step 'apply discount WELCOME10' {
    $r = Post-Form '/cart/discount' @{ code = 'WELCOME10'; returnUrl = '/cart' } '/cart'
    if ($r.Content -notmatch 'WELCOME10') { throw 'discount code not reflected on cart page' }
}

Step 'register new customer' {
    $r = Post-Form '/account/register' @{
        'Register.FirstName' = 'Smoke'; 'Register.LastName' = 'Tester'; 'Register.Email' = $email
        'Register.PhoneCountryCode' = '+965'; 'Register.Phone' = '50000000'
        'Register.Password' = $password; 'Register.EmailOptIn' = 'true'
        'Register.WhatsAppOptIn' = 'false'; 'Register.AcceptTerms' = 'true'
    } '/account/login'
    if ($r.BaseResponse.ResponseUri.AbsolutePath -like '*/login*') { throw 'still on login page after register' }
}

Step 'account dashboard reachable after register' {
    $r = Get-Page '/account'
    if ($r.BaseResponse.ResponseUri.AbsolutePath -like '*/login*') { throw 'redirected to login — not authenticated' }
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'checkout renders with cart (merged after login)' {
    $r = Get-Page '/checkout'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
    if ($r.BaseResponse.ResponseUri.AbsolutePath -eq '/cart') { throw 'cart was empty at checkout — guest merge failed' }
}

Step 'place order' {
    $r = Post-Form '/checkout/place' @{
        email = $email; phone = '+96550000000'; firstName = 'Smoke'; lastName = 'Tester'
        governorate = 'Al Asimah'; area = 'Kuwait City'; block = '3'; street = 'Salem Al Mubarak'
        building = '12'; floor = '2'; apartment = '4'; directions = 'Test order'
        shippingMethod = 'standard'; paymentMethod = 'cod'; termsAccepted = 'true'
    } '/checkout'
    $m = [regex]::Match($r.Content, '(WS-\d+)')
    if (-not $m.Success) { throw 'no order number on confirmation' }
    $script:orderNumber = $m.Groups[1].Value
}

Step 'order appears in account orders' {
    $r = Get-Page '/account/orders'
    if ($r.Content -notmatch [regex]::Escape($script:orderNumber)) { throw "order $script:orderNumber not listed" }
}

Step 'order detail renders' {
    $r = Get-Page "/account/orders/$script:orderNumber"
    if ($r.BaseResponse.ResponseUri.AbsolutePath -like '*/login*') { throw 'redirected to login' }
    if ($r.Content -notmatch [regex]::Escape($script:orderNumber)) { throw 'order number missing from detail page' }
}

Step 'save address' {
    $r = Post-Form '/account/addresses/save' @{
        label = 'Home'; firstName = 'Smoke'; lastName = 'Tester'; phone = '+96550000000'
        governorate = 'Hawalli'; area = 'Salmiya'; block = '10'; street = 'Baghdad St'; building = '5'
    } '/account/addresses'
    $check = Get-Page '/account/addresses'
    if ($check.Content -notmatch 'Salmiya') { throw 'saved address not shown' }
}

Step 'wishlist add + list' {
    if (-not $script:productId) {
        $pdp = Get-Page "/products/$script:productSlug"
        $p = [regex]::Match($pdp.Content, 'name="productId"[^>]*value="(\d+)"')
        if (-not $p.Success) { throw 'no productId form field found for wishlist' }
        $script:productId = $p.Groups[1].Value
    }
    Post-Form '/account/wishlist/add' @{ productId = $script:productId; returnUrl = '/account/wishlist' } "/products/$script:productSlug" | Out-Null
    $r = Get-Page '/account/wishlist'
    if ($r.Content -notmatch '/products/') { throw 'wishlist appears empty' }
}

Step 'contact form persists' {
    $r = Post-Form '/contact' @{ name = 'Smoke Tester'; email = $email; subject = 'Test'; message = 'E2E smoke message' } '/contact'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'newsletter subscribe' {
    $r = Post-Form '/newsletter' @{ email = "news$stamp@test.whitestiches.kw"; source = 'home' } '/'
    if ($r.StatusCode -ne 200) { throw "status $($r.StatusCode)" }
}

Step 'track order by number + email' {
    $r = Post-Form '/track' @{ orderNumber = $script:orderNumber; contact = $email } '/track'
    if ($r.Content -notmatch [regex]::Escape($script:orderNumber)) { throw 'tracking result missing order' }
}

Step 'journal index + post' {
    $r = Get-Page '/journal'
    $m = [regex]::Match($r.Content, 'href="/journal/([a-z0-9-]+)"')
    if (-not $m.Success) { throw 'no journal post links' }
    $post = Get-Page "/journal/$($m.Groups[1].Value)"
    if ($post.StatusCode -ne 200) { throw 'post failed' }
}

Step 'logout' {
    $r = Post-Form '/account/logout' @{} '/account'
    $after = Invoke-WebRequest -Uri "$BaseUrl/account" -WebSession $session -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($after.StatusCode -eq 200) { throw 'still authenticated after logout' }
}

Write-Host ''
if ($script:failures.Count -eq 0) {
    Write-Host 'E2E SMOKE: ALL PASS'
} else {
    Write-Host ("E2E SMOKE: {0} FAILURE(S): {1}" -f $script:failures.Count, ($script:failures -join '; '))
    exit 1
}

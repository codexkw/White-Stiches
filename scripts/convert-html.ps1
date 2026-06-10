# ============================================================================
# convert-html.ps1 — one-shot mechanical conversion of the static HTML site
# into Razor views for WhiteStiches.Web.
#
# Per-page transform:
#   1. Escape all Razor-significant '@' as '@@'
#   2. Rewrite asset refs  (css/ js/ assets/  ->  ~/css/ ~/js/ ~/assets/)
#   3. Rewrite quoted *.html links to MVC routes
#   4. Extract page-specific content between the shared-shell markers
#   5. Emit .cshtml with ViewData Title/MetaDescription from the source <head>
#
# Layouts are generated from index.html (full shell) and checkout.html
# (document shell only — the .co-hdr header and .co-steps differ per page
# and therefore stay inside each view).
# ============================================================================

$ErrorActionPreference = 'Stop'

$root  = 'C:\Users\anas-\source\repos\White-Stiches'
$html  = Join-Path $root 'HTML'
$views = Join-Path $root 'src\WhiteStiches.Web\Views'

$utf8Bom = New-Object System.Text.UTF8Encoding($true)

# ---------------------------------------------------------------- route map
$routeMap = @{
    'index.html'                = '/'
    'intro.html'                = '/intro'
    'collection.html'           = '/collection'
    'product.html'              = '/product'
    'search.html'               = '/search'
    'cart.html'                 = '/cart'
    'checkout.html'             = '/checkout'
    'order-confirmation.html'   = '/checkout/confirmation'
    'account.html'              = '/account'
    'account-login.html'        = '/account/login'
    'account-orders.html'       = '/account/orders'
    'account-order-detail.html' = '/account/orders/detail'
    'account-addresses.html'    = '/account/addresses'
    'account-profile.html'      = '/account/profile'
    'account-wishlist.html'     = '/account/wishlist'
    'account-returns.html'      = '/account/returns'
    'about.html'                = '/about'
    'contact.html'              = '/contact'
    'faq.html'                  = '/faq'
    'size-guide.html'           = '/size-guide'
    'shipping.html'             = '/shipping'
    'returns-policy.html'       = '/returns-policy'
    'privacy.html'              = '/privacy'
    'terms.html'                = '/terms'
    'cookies.html'              = '/cookies'
    'track.html'                = '/track'
    'journal.html'              = '/journal'
    'journal-post.html'         = '/journal/post'
    'not-found.html'            = '/not-found'
    'maintenance.html'          = '/maintenance'
    'design-system.html'        = '/design-system'
}

# file -> view path (relative to Views\) + layout kind
$pages = @(
    @{ File = 'index.html';                View = 'Home\Index.cshtml';          Kind = 'standard'   }
    @{ File = 'not-found.html';            View = 'Home\NotFound.cshtml';       Kind = 'standard'   }
    @{ File = 'intro.html';                View = 'Home\Intro.cshtml';          Kind = 'standalone' }
    @{ File = 'maintenance.html';          View = 'Home\Maintenance.cshtml';    Kind = 'standalone' }
    @{ File = 'design-system.html';        View = 'Home\DesignSystem.cshtml';   Kind = 'standalone' }
    @{ File = 'collection.html';           View = 'Shop\Collection.cshtml';     Kind = 'standard'   }
    @{ File = 'product.html';              View = 'Shop\Product.cshtml';        Kind = 'standard'   }
    @{ File = 'search.html';               View = 'Shop\Search.cshtml';         Kind = 'standard'   }
    @{ File = 'cart.html';                 View = 'Cart\Index.cshtml';          Kind = 'standard'   }
    @{ File = 'checkout.html';             View = 'Checkout\Index.cshtml';      Kind = 'checkout'   }
    @{ File = 'order-confirmation.html';   View = 'Checkout\Confirmation.cshtml'; Kind = 'checkout' }
    @{ File = 'account.html';              View = 'Account\Index.cshtml';       Kind = 'standard'   }
    @{ File = 'account-login.html';        View = 'Account\Login.cshtml';       Kind = 'standard'   }
    @{ File = 'account-orders.html';       View = 'Account\Orders.cshtml';      Kind = 'standard'   }
    @{ File = 'account-order-detail.html'; View = 'Account\OrderDetail.cshtml'; Kind = 'standard'   }
    @{ File = 'account-addresses.html';    View = 'Account\Addresses.cshtml';   Kind = 'standard'   }
    @{ File = 'account-profile.html';      View = 'Account\Profile.cshtml';     Kind = 'standard'   }
    @{ File = 'account-wishlist.html';     View = 'Account\Wishlist.cshtml';    Kind = 'standard'   }
    @{ File = 'account-returns.html';      View = 'Account\Returns.cshtml';     Kind = 'standard'   }
    @{ File = 'about.html';                View = 'Pages\About.cshtml';         Kind = 'standard'   }
    @{ File = 'contact.html';              View = 'Pages\Contact.cshtml';       Kind = 'standard'   }
    @{ File = 'faq.html';                  View = 'Pages\Faq.cshtml';           Kind = 'standard'   }
    @{ File = 'size-guide.html';           View = 'Pages\SizeGuide.cshtml';     Kind = 'standard'   }
    @{ File = 'shipping.html';             View = 'Pages\Shipping.cshtml';      Kind = 'standard'   }
    @{ File = 'returns-policy.html';       View = 'Pages\ReturnsPolicy.cshtml'; Kind = 'standard'   }
    @{ File = 'privacy.html';              View = 'Pages\Privacy.cshtml';       Kind = 'standard'   }
    @{ File = 'terms.html';                View = 'Pages\Terms.cshtml';         Kind = 'standard'   }
    @{ File = 'cookies.html';              View = 'Pages\Cookies.cshtml';       Kind = 'standard'   }
    @{ File = 'track.html';                View = 'Pages\Track.cshtml';         Kind = 'standard'   }
    @{ File = 'journal.html';              View = 'Journal\Index.cshtml';       Kind = 'standard'   }
    @{ File = 'journal-post.html';         View = 'Journal\Post.cshtml';        Kind = 'standard'   }
)

# shared-shell markers
# Page content begins after the mobile drawer's closing tag — the drawer-overlay div
# precedes the <aside class="drawer"> in the source, and both belong to the layout.
$shellContentStart   = '</aside>'
$shellContentEnd     = '<footer class="ftr">'
$coContentStart      = '<header class="co-hdr">'
$coContentEnd        = '<footer class="co-footer">'

# ---------------------------------------------------------------- helpers
function Convert-Markup {
    param([string]$Text)

    # 1. escape Razor '@'
    $t = $Text.Replace('@', '@@')

    # 2. asset refs -> app-relative (tag helpers resolve ~/ in href/src/poster)
    $t = [regex]::Replace($t, '(href|src|poster)="(css|js|assets)/', '$1="~/$2/')
    # url(...) inside inline styles cannot use ~/
    $t = [regex]::Replace($t, "url\((['""]?)(?:\./)?assets/", 'url($1/assets/')

    # 3. quoted *.html links -> MVC routes (handles "x.html", 'x.html', `x.html`,
    #    plus trailing #anchor or ?query)
    $eval = {
        param($m)
        $q1   = $m.Groups[1].Value
        $file = $m.Groups[2].Value
        $rest = $m.Groups[3].Value
        $q2   = $m.Groups[4].Value
        if ($routeMap.ContainsKey($file)) {
            return $q1 + $routeMap[$file] + $rest + $q2
        }
        return $m.Value
    }
    $t = [regex]::Replace($t, '(["''`])([a-z0-9-]+\.html)([#?][^"''`]*)?(["''`])', $eval)

    return $t
}

function Get-HeadMeta {
    param([string]$Raw)
    $title = ''
    $desc  = ''
    $m = [regex]::Match($Raw, '<title>(.*?)</title>', 'Singleline')
    if ($m.Success) { $title = $m.Groups[1].Value.Trim() }
    $m = [regex]::Match($Raw, '<meta name="description" content="(.*?)"')
    if ($m.Success) { $desc = $m.Groups[1].Value.Trim() }
    # Decode entities (&amp; etc.) — Razor re-encodes ViewData on render
    $title = [System.Net.WebUtility]::HtmlDecode($title)
    $desc  = [System.Net.WebUtility]::HtmlDecode($desc)
    return @{ Title = $title; Description = $desc }
}

function Get-Between {
    param([string]$Raw, [string]$StartMarker, [string]$EndMarker, [string]$File)
    $start = $Raw.IndexOf($StartMarker)
    if ($start -lt 0) { throw "Marker start not found in ${File}: $StartMarker" }
    $start += $StartMarker.Length
    $end = $Raw.IndexOf($EndMarker, $start)
    if ($end -lt 0) { throw "Marker end not found in ${File}: $EndMarker" }
    return $Raw.Substring($start, $end - $start).Trim("`r", "`n")
}

function Write-View {
    param([string]$RelPath, [string]$Content)
    $full = Join-Path $views $RelPath
    $dir  = Split-Path $full -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($full, $Content, $utf8Bom)
    Write-Host "  wrote $RelPath"
}

function Escape-CSharpString {
    param([string]$s)
    return $s.Replace('\', '\\').Replace('"', '\"')
}

# ---------------------------------------------------------------- layouts
Write-Host "Generating layouts..."

# _Layout.cshtml — full storefront shell from index.html
$indexRaw = [System.IO.File]::ReadAllText((Join-Path $html 'index.html'))

$heroIdx   = $indexRaw.IndexOf('<section class="hero">')
$footerIdx = $indexRaw.IndexOf($shellContentEnd)
if ($heroIdx -lt 0 -or $footerIdx -lt 0) { throw 'index.html shell markers not found' }

$top    = Convert-Markup ($indexRaw.Substring(0, $heroIdx))
$bottom = Convert-Markup ($indexRaw.Substring($footerIdx))

# title + meta description become ViewData-driven
$top = [regex]::Replace($top, '<title>.*?</title>', '<title>@ViewData["Title"]</title>', 'Singleline')
$top = [regex]::Replace($top, '<meta name="description" content=".*?" />', '<meta name="description" content="@ViewData["MetaDescription"]" />')

# optional per-view scripts before </body>
$bottom = $bottom.Replace('</body>', "@RenderSection(""Scripts"", required: false)`r`n</body>")

$layout = $top + "@RenderBody()`r`n`r`n" + $bottom
Write-View 'Shared\_Layout.cshtml' $layout

# _CheckoutLayout.cshtml — document shell only (header + steps live in views)
$checkoutRaw = [System.IO.File]::ReadAllText((Join-Path $html 'checkout.html'))

$coHdrIdx    = $checkoutRaw.IndexOf($coContentStart)
$coFooterIdx = $checkoutRaw.IndexOf($coContentEnd)
if ($coHdrIdx -lt 0 -or $coFooterIdx -lt 0) { throw 'checkout.html shell markers not found' }

$coTop    = Convert-Markup ($checkoutRaw.Substring(0, $coHdrIdx))
$coBottom = Convert-Markup ($checkoutRaw.Substring($coFooterIdx))

$coTop = [regex]::Replace($coTop, '<title>.*?</title>', '<title>@ViewData["Title"]</title>', 'Singleline')
$coTop = [regex]::Replace($coTop, '<meta name="description" content=".*?" />', '<meta name="description" content="@ViewData["MetaDescription"]" />')
# drop the stale "MINIMAL CHECKOUT HEADER" banner comment dangling at the cut
$coTop = [regex]::Replace($coTop, '<!-- =+ MINIMAL CHECKOUT HEADER =+ -->\s*$', '')

$coBottom = $coBottom.Replace('</body>', "@RenderSection(""Scripts"", required: false)`r`n</body>")

$coLayout = $coTop.TrimEnd() + "`r`n`r`n@RenderBody()`r`n`r`n<!-- ============================ MINIMAL FOOTER ============================ -->`r`n" + $coBottom
Write-View 'Shared\_CheckoutLayout.cshtml' $coLayout

# ---------------------------------------------------------------- pages
Write-Host "Converting pages..."

foreach ($page in $pages) {
    $raw  = [System.IO.File]::ReadAllText((Join-Path $html $page.File))
    $meta = Get-HeadMeta $raw
    $title = Escape-CSharpString $meta.Title
    $desc  = Escape-CSharpString $meta.Description

    switch ($page.Kind) {
        'standard' {
            $content = Get-Between $raw $shellContentStart $shellContentEnd $page.File
            $content = Convert-Markup $content
            $view = "@{`r`n    ViewData[""Title""] = ""$title"";`r`n    ViewData[""MetaDescription""] = ""$desc"";`r`n}`r`n`r`n" + $content + "`r`n"
            Write-View $page.View $view
        }
        'checkout' {
            $content = Get-Between $raw $coContentStart $coContentEnd $page.File
            $content = $coContentStart + $content   # marker is the element itself — keep it
            $content = Convert-Markup $content
            $view = "@{`r`n    Layout = ""_CheckoutLayout"";`r`n    ViewData[""Title""] = ""$title"";`r`n    ViewData[""MetaDescription""] = ""$desc"";`r`n}`r`n`r`n" + $content + "`r`n"
            Write-View $page.View $view
        }
        'standalone' {
            $content = Convert-Markup $raw
            $view = "@{`r`n    Layout = null;`r`n}`r`n" + $content
            Write-View $page.View $view
        }
    }
}

# ---------------------------------------------------------------- site.js link patch
Write-Host "Patching wwwroot/js/site.js hardcoded links..."
$siteJsPath = Join-Path $root 'src\WhiteStiches.Web\wwwroot\js\site.js'
$siteJs = [System.IO.File]::ReadAllText($siteJsPath)
$evalJs = {
    param($m)
    $q1   = $m.Groups[1].Value
    $file = $m.Groups[2].Value
    $rest = $m.Groups[3].Value
    $q2   = $m.Groups[4].Value
    if ($routeMap.ContainsKey($file)) { return $q1 + $routeMap[$file] + $rest + $q2 }
    return $m.Value
}
$siteJs = [regex]::Replace($siteJs, '(["''`])([a-z0-9-]+\.html)([#?][^"''`]*)?(["''`])', $evalJs)
# plain .js gets no BOM — keep it byte-faithful to the source
[System.IO.File]::WriteAllText($siteJsPath, $siteJs, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Done."

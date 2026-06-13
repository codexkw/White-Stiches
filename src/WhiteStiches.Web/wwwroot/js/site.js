/* ============================================================================
   WHITE STITCHES — Site Behaviors
   Mega-menu, scroll reveals, language toggle, sticky header
============================================================================ */

document.addEventListener('DOMContentLoaded', () => {

  /* ──────────────────────────────────────────────────────────
     MEGA MENU — hover (desktop) + click (touch / accessibility)
     ────────────────────────────────────────────────────────── */
  const menuTriggers = document.querySelectorAll('[data-menu]');
  const megamenus = document.querySelectorAll('.megamenu');
  const hdr = document.getElementById('hdr');

  let openMenuKey = null;
  let closeTimer = null;

  function openMenu(key) {
    clearTimeout(closeTimer);

    // Close any other open menu
    megamenus.forEach(m => {
      if (m.dataset.menuPanel !== key) m.classList.remove('is-open');
    });
    menuTriggers.forEach(t => {
      t.classList.toggle('is-active', t.dataset.menu === key);
    });

    // Open the requested menu
    const target = document.querySelector(`[data-menu-panel="${key}"]`);
    if (target) {
      target.classList.add('is-open');
      openMenuKey = key;
    }
  }

  function closeMenu() {
    megamenus.forEach(m => m.classList.remove('is-open'));
    menuTriggers.forEach(t => t.classList.remove('is-active'));
    openMenuKey = null;
  }

  function scheduleClose() {
    closeTimer = setTimeout(closeMenu, 180);
  }

  // Hover triggers (desktop)
  menuTriggers.forEach(trigger => {
    const key = trigger.dataset.menu;

    trigger.addEventListener('mouseenter', () => openMenu(key));
    trigger.addEventListener('focus', () => openMenu(key));

    // Click toggles (also handles touch)
    trigger.addEventListener('click', (e) => {
      e.preventDefault();
      if (openMenuKey === key) {
        closeMenu();
      } else {
        openMenu(key);
      }
    });
  });

  // Keep menu open while hovering the panel itself
  megamenus.forEach(menu => {
    menu.addEventListener('mouseenter', () => clearTimeout(closeTimer));
    menu.addEventListener('mouseleave', scheduleClose);
  });

  // Close when leaving the entire header region
  if (hdr) {
    hdr.addEventListener('mouseleave', scheduleClose);
  }

  // Close on Escape
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && openMenuKey) closeMenu();
  });

  // Close when clicking outside
  document.addEventListener('click', (e) => {
    if (openMenuKey &&
        !e.target.closest('[data-menu]') &&
        !e.target.closest('.megamenu')) {
      closeMenu();
    }
  });

  /* ──────────────────────────────────────────────────────────
     SCROLL-TRIGGERED REVEALS
     ────────────────────────────────────────────────────────── */
  document.querySelectorAll(
    '.section-head, .cat-circle, .product, .editorial__media, .editorial__copy, .value, .ig__tile, .newsletter__inner'
  ).forEach((el, i) => {
    el.classList.add('reveal');
    el.style.transitionDelay = `${Math.min(i * 40, 400)}ms`;
  });

  const io = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('is-in');
        io.unobserve(entry.target);
      }
    });
  }, { threshold: 0.12, rootMargin: '0px 0px -60px 0px' });

  document.querySelectorAll('.reveal').forEach(el => io.observe(el));

  /* ──────────────────────────────────────────────────────────
     LANGUAGE — the switcher is now a real server round-trip
     (/set-culture writes the culture cookie and the server renders
     <html dir>/text in the chosen language). The old client-side
     dir-flip preview was removed in Phase 1E‑3 so it can't fight
     the server-rendered direction. Nothing to wire up here.
     ────────────────────────────────────────────────────────── */

  /* ──────────────────────────────────────────────────────────
     HEADER — sticky behavior is handled by CSS position:sticky.
     We no longer shrink on scroll because the full-logo image
     needs consistent header height.
     ────────────────────────────────────────────────────────── */

  /* ──────────────────────────────────────────────────────────
     MOBILE DRAWER — hamburger toggle, overlay click, ESC close
     iOS Safari needs scroll-position preservation when body is
     locked, otherwise the page jumps to top when the drawer closes.
     ────────────────────────────────────────────────────────── */
  const burger = document.getElementById('hdr-burger');
  const drawer = document.getElementById('mobile-drawer');
  const drawerOverlay = document.getElementById('drawer-overlay');
  const drawerClose = document.getElementById('drawer-close');

  let savedScrollY = 0;

  function openDrawer() {
    if (!drawer) return;
    savedScrollY = window.scrollY;
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    drawerOverlay.classList.add('is-open');
    drawerOverlay.setAttribute('aria-hidden', 'false');
    burger.classList.add('is-open');
    burger.setAttribute('aria-expanded', 'true');
    document.body.classList.add('drawer-open');
    document.body.style.top = `-${savedScrollY}px`;
  }

  function closeDrawer() {
    if (!drawer) return;
    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    drawerOverlay.classList.remove('is-open');
    drawerOverlay.setAttribute('aria-hidden', 'true');
    burger.classList.remove('is-open');
    burger.setAttribute('aria-expanded', 'false');
    document.body.classList.remove('drawer-open');
    document.body.style.top = '';
    // Restore scroll position so iOS doesn't snap back to top
    window.scrollTo(0, savedScrollY);
  }

  if (burger) {
    burger.addEventListener('click', () => {
      if (drawer.classList.contains('is-open')) closeDrawer();
      else openDrawer();
    });
  }
  if (drawerClose) drawerClose.addEventListener('click', closeDrawer);
  if (drawerOverlay) drawerOverlay.addEventListener('click', closeDrawer);

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && drawer && drawer.classList.contains('is-open')) {
      closeDrawer();
    }
  });

  // Close drawer if viewport grows past the mobile breakpoint while open
  window.addEventListener('resize', () => {
    if (window.innerWidth > 900 && drawer && drawer.classList.contains('is-open')) {
      closeDrawer();
    }
  });

  /* ──────────────────────────────────────────────────────────
     DRAWER COLLAPSIBLES — Women / Men / Accessories
     Pure JS toggle (replaced <details> because of iOS click bugs)
     ────────────────────────────────────────────────────────── */
  document.querySelectorAll('.drawer__toggle').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      const sub = btn.nextElementSibling;
      const isOpen = btn.classList.contains('is-open');

      // Optionally close other groups (accordion behavior — comment out if not desired)
      document.querySelectorAll('.drawer__toggle').forEach(other => {
        if (other !== btn) {
          other.classList.remove('is-open');
          other.setAttribute('aria-expanded', 'false');
          if (other.nextElementSibling) other.nextElementSibling.classList.remove('is-open');
        }
      });

      if (isOpen) {
        btn.classList.remove('is-open');
        btn.setAttribute('aria-expanded', 'false');
        sub.classList.remove('is-open');
      } else {
        btn.classList.add('is-open');
        btn.setAttribute('aria-expanded', 'true');
        sub.classList.add('is-open');
      }
    });
  });
});

/* ============================================================================
   PRODUCT DETAIL PAGE — runs only on PDP
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const pdp = document.querySelector('.pdp');
  if (!pdp) return;

  /* ── GALLERY: thumbnail click → main slide ───────────────────────── */
  const thumbs = document.querySelectorAll('.pdp__thumb');
  const slides = document.querySelectorAll('.pdp__slide');
  const dots = document.querySelectorAll('.pdp__dot');
  const counterCurrent = document.querySelector('.pdp__counter-current');

  function showSlide(idx) {
    thumbs.forEach(t => {
      const isMatch = parseInt(t.dataset.index) === idx;
      t.classList.toggle('is-active', isMatch);
      t.setAttribute('aria-selected', isMatch ? 'true' : 'false');
    });
    slides.forEach(s => s.classList.toggle('is-active', parseInt(s.dataset.index) === idx));
    dots.forEach((d, i) => d.classList.toggle('is-active', i === idx));
    if (counterCurrent) counterCurrent.textContent = String(idx + 1).padStart(2, '0');

    // Pause any non-active videos
    slides.forEach(s => {
      const v = s.querySelector('video');
      if (v && !s.classList.contains('is-active')) v.pause();
    });
  }

  thumbs.forEach(t => {
    t.addEventListener('click', () => showSlide(parseInt(t.dataset.index)));
  });

  /* ── GALLERY: swipe on mobile main image ─────────────────────────── */
  const mainArea = document.querySelector('.pdp__main');
  if (mainArea) {
    let startX = 0;
    mainArea.addEventListener('touchstart', e => { startX = e.touches[0].clientX; }, { passive: true });
    mainArea.addEventListener('touchend', e => {
      const dx = e.changedTouches[0].clientX - startX;
      if (Math.abs(dx) < 40) return;
      const active = document.querySelector('.pdp__slide.is-active');
      const idx = parseInt(active.dataset.index);
      if (dx < 0 && idx < slides.length - 1) showSlide(idx + 1);
      if (dx > 0 && idx > 0) showSlide(idx - 1);
    }, { passive: true });
  }

  /* ── COLOR VARIANT picker ────────────────────────────────────────── */
  const colorBtns = document.querySelectorAll('.color-thumb');
  const colorValue = document.getElementById('colorValue');
  colorBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      if (btn.classList.contains('is-soldout')) return;
      colorBtns.forEach(b => {
        b.classList.remove('is-active');
        b.setAttribute('aria-checked', 'false');
      });
      btn.classList.add('is-active');
      btn.setAttribute('aria-checked', 'true');
      if (colorValue) colorValue.textContent = btn.dataset.color;
    });
  });

  /* ── SIZE chips ──────────────────────────────────────────────────── */
  const sizeBtns = document.querySelectorAll('.size-chip');
  sizeBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      if (btn.classList.contains('is-soldout')) return;
      sizeBtns.forEach(b => {
        b.classList.remove('is-active');
        b.setAttribute('aria-checked', 'false');
      });
      btn.classList.add('is-active');
      btn.setAttribute('aria-checked', 'true');
    });
  });

  /* ── QUANTITY stepper ────────────────────────────────────────────── */
  const qtyInput = document.getElementById('qtyInput');
  const qtyMinus = document.getElementById('qtyMinus');
  const qtyPlus = document.getElementById('qtyPlus');
  if (qtyMinus && qtyPlus && qtyInput) {
    qtyMinus.addEventListener('click', () => {
      const v = Math.max(1, parseInt(qtyInput.value) - 1);
      qtyInput.value = v;
    });
    qtyPlus.addEventListener('click', () => {
      const v = Math.min(10, parseInt(qtyInput.value) + 1);
      qtyInput.value = v;
    });
  }

  /* ── WISHLIST toggle ─────────────────────────────────────────────── */
  const wishlist = document.getElementById('wishlistBtn');
  if (wishlist) {
    wishlist.addEventListener('click', () => {
      const pressed = wishlist.getAttribute('aria-pressed') === 'true';
      wishlist.setAttribute('aria-pressed', pressed ? 'false' : 'true');
    });
  }

  /* ── ADD TO CART (demo) ──────────────────────────────────────────── */
  const atc = document.getElementById('addToCart');
  if (atc) {
    atc.addEventListener('click', () => {
      const original = atc.innerHTML;
      atc.innerHTML = '<span>Added ✓</span>';
      setTimeout(() => { atc.innerHTML = original; }, 1600);
    });
  }

  /* ── MOBILE STICKY BAR — show after scrolling past inline ATC ────── */
  const stickybar = document.getElementById('pdpStickybar');
  if (stickybar && atc) {
    document.body.classList.add('has-pdp-stickybar');
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        // Show the stickybar when the inline ATC is OUT of view
        stickybar.classList.toggle('is-visible', !entry.isIntersecting);
        stickybar.setAttribute('aria-hidden', entry.isIntersecting ? 'true' : 'false');
      });
    }, { threshold: 0 });
    observer.observe(atc);
  }
});

/* ============================================================================
   COLLECTION PAGE — runs only on collection
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const coll = document.querySelector('.coll');
  if (!coll) return;

  const filterSidebar = document.getElementById('filterSidebar');
  const filterToggle = document.getElementById('filterToggle');
  const filterClose = document.getElementById('filterClose');
  const filterOverlay = document.getElementById('filterOverlay');
  const filterChips = document.getElementById('filterChips');
  const filterCount = document.getElementById('activeFilterCount');
  const filterClear = document.getElementById('filterClear');
  const filterApply = document.getElementById('filterApply');
  const emptyClear = document.getElementById('emptyClear');
  const sortSelect = document.getElementById('sortSelect');
  const resultCount = document.getElementById('resultCount');
  const collMain = document.querySelector('.coll__main');

  let savedScrollY = 0;

  /* ── MOBILE FILTER DRAWER ────────────────────────────────────────── */
  function openFilter() {
    savedScrollY = window.scrollY;
    filterSidebar.classList.add('is-open');
    filterOverlay.classList.add('is-open');
    filterOverlay.setAttribute('aria-hidden', 'false');
    filterToggle.setAttribute('aria-expanded', 'true');
    document.body.classList.add('filter-open');
    document.body.style.top = `-${savedScrollY}px`;
  }
  function closeFilter() {
    filterSidebar.classList.remove('is-open');
    filterOverlay.classList.remove('is-open');
    filterOverlay.setAttribute('aria-hidden', 'true');
    filterToggle.setAttribute('aria-expanded', 'false');
    document.body.classList.remove('filter-open');
    document.body.style.top = '';
    window.scrollTo(0, savedScrollY);
  }
  if (filterToggle) {
    filterToggle.addEventListener('click', () => {
      if (filterSidebar.classList.contains('is-open')) closeFilter();
      else openFilter();
    });
  }
  if (filterClose) filterClose.addEventListener('click', closeFilter);
  if (filterOverlay) filterOverlay.addEventListener('click', closeFilter);
  if (filterApply) filterApply.addEventListener('click', closeFilter);
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && filterSidebar.classList.contains('is-open')) closeFilter();
  });

  /* ── FILTER STATE — collect checked items + render chips ─────────── */
  const labels = {
    category: 'Category', size: 'Size', color: 'Colour',
    price: 'Price', fabric: 'Fabric', occasion: 'Occasion', availability: 'Availability'
  };

  function getActiveFilters() {
    const inputs = filterSidebar.querySelectorAll('input[type="checkbox"]:checked');
    return Array.from(inputs).map(input => ({
      group: input.name,
      value: input.value,
      label: input.closest('label')?.querySelector('span, em')?.textContent.split(/\s\d/)[0].trim() || input.value
    }));
  }

  function renderChips() {
    const active = getActiveFilters();
    filterChips.innerHTML = '';
    if (active.length === 0) {
      filterChips.hidden = true;
      filterCount.hidden = true;
      filterCount.textContent = '0';
      return;
    }
    filterChips.hidden = false;
    filterCount.hidden = false;
    filterCount.textContent = String(active.length);
    active.forEach(f => {
      const chip = document.createElement('span');
      chip.className = 'chip';
      chip.innerHTML = `<span>${f.label}</span><button class="chip__remove" type="button" aria-label="Remove ${f.label} filter" data-group="${f.group}" data-value="${f.value}"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M6 6l12 12M18 6L6 18"/></svg></button>`;
      filterChips.appendChild(chip);
    });
  }

  // Listen for any checkbox change inside the sidebar
  filterSidebar.addEventListener('change', (e) => {
    if (e.target.matches('input[type="checkbox"]')) {
      renderChips();
      // In a real app: trigger a filtered fetch / URL update here
    }
  });

  // Remove an individual chip
  filterChips.addEventListener('click', (e) => {
    const btn = e.target.closest('.chip__remove');
    if (!btn) return;
    const { group, value } = btn.dataset;
    const cb = filterSidebar.querySelector(`input[name="${group}"][value="${value}"]`);
    if (cb) {
      cb.checked = false;
      renderChips();
    }
  });

  // Clear all
  function clearAllFilters() {
    filterSidebar.querySelectorAll('input[type="checkbox"]:checked').forEach(cb => cb.checked = false);
    filterSidebar.querySelectorAll('input[type="number"]').forEach(i => i.value = '');
    renderChips();
    collMain.classList.remove('is-empty');
  }
  if (filterClear) filterClear.addEventListener('click', clearAllFilters);
  if (emptyClear) emptyClear.addEventListener('click', clearAllFilters);

  /* ── SORT — update count display, simulate state ─────────────────── */
  if (sortSelect) {
    sortSelect.addEventListener('change', () => {
      // Real app would re-fetch sorted results.
      // For demo: brief skeleton flash to confirm the interaction.
      collMain.classList.add('is-loading');
      setTimeout(() => collMain.classList.remove('is-loading'), 600);
    });
  }
});

/* ============================================================================
   SEARCH RESULTS PAGE — runs only on /search
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const searchHead = document.querySelector('.search-head');
  if (!searchHead) return;

  const input = document.getElementById('searchInput');
  const clearBtn = document.getElementById('searchClear');
  const refineChips = document.querySelectorAll('.search-refine__chip');
  const collMain = document.querySelector('.coll__main');
  const searchEmptyQuery = document.getElementById('searchEmptyQuery');
  const suggestionBtn = document.getElementById('suggestionBtn');

  /* ── INPUT: show/hide clear button based on value ────────────────── */
  function syncClearBtn() {
    if (!input || !clearBtn) return;
    clearBtn.classList.toggle('is-visible', input.value.trim().length > 0);
  }
  syncClearBtn();
  if (input) input.addEventListener('input', syncClearBtn);

  if (clearBtn && input) {
    clearBtn.addEventListener('click', () => {
      input.value = '';
      syncClearBtn();
      input.focus();
    });
  }

  /* ── SUBMIT: in a real app, push to /search?q=… and re-fetch.
      For the static demo, just refocus the input. */
  window.handleSearchSubmit = function() {
    if (input) input.blur();
  };

  /* ── "Did you mean" suggestion: clicking fills input and submits ─── */
  if (suggestionBtn && input) {
    suggestionBtn.addEventListener('click', () => {
      input.value = suggestionBtn.textContent.trim();
      syncClearBtn();
      input.focus();
    });
  }

  /* ── REFINE chips: switch active tab. In a real app, this would
      filter results to that type (Products / Collections / Journal). */
  refineChips.forEach(chip => {
    chip.addEventListener('click', () => {
      refineChips.forEach(c => {
        c.classList.remove('is-active');
        c.setAttribute('aria-selected', 'false');
      });
      chip.classList.add('is-active');
      chip.setAttribute('aria-selected', 'true');
      // Brief skeleton flash to confirm the switch
      if (collMain) {
        collMain.classList.add('is-loading');
        setTimeout(() => collMain.classList.remove('is-loading'), 500);
      }
    });
  });

  /* ── DEMO: keyboard shortcut "z" toggles the zero-results state
      so you can preview the empty UI without typing a real misspelling. */
  document.addEventListener('keydown', (e) => {
    if (e.key === 'z' && !e.ctrlKey && !e.metaKey && !e.altKey &&
        document.activeElement.tagName !== 'INPUT') {
      if (collMain) {
        collMain.classList.toggle('is-empty');
        if (searchEmptyQuery && input) {
          searchEmptyQuery.textContent = '"' + (input.value || 'your search') + '"';
        }
      }
    }
  });
});

/* ============================================================================
   CART PAGE — runs only on /cart
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const cart = document.querySelector('.cart');
  // Server-rendered cart owns totals now — demo module needs its #subtotal hook to run.
  if (!cart || !document.getElementById('subtotal')) return;

  const FREE_SHIP_THRESHOLD = 50;

  // ── Recalculate everything on any qty/remove/discount change
  function fmt(n) { return n.toFixed(3); }

  function recompute() {
    let subtotal = 0;
    let itemCount = 0;
    document.querySelectorAll('.cart-item').forEach(item => {
      const input = item.querySelector('.qty__input');
      const unit = parseFloat(input.dataset.unitPrice);
      const qty = parseInt(input.value) || 1;
      const line = unit * qty;
      const lineEl = item.querySelector('[data-line-total]');
      if (lineEl) lineEl.textContent = fmt(line) + ' KWD';
      subtotal += line;
      itemCount += qty;
    });

    // Gift wrap
    const giftWrap = document.getElementById('giftWrap');
    if (giftWrap && giftWrap.checked) subtotal += 3.5;

    // Discount
    const discountAmt = parseFloat(document.getElementById('discountAmt')?.dataset.amount || '0');
    const discounted = Math.max(0, subtotal - discountAmt);

    // Shipping
    const free = discounted >= FREE_SHIP_THRESHOLD;
    const shipping = free ? 0 : 2;
    const grand = discounted + shipping;

    document.getElementById('subtotal').textContent = fmt(subtotal) + ' KWD';
    document.getElementById('shipping').textContent = free ? 'Free' : (fmt(shipping) + ' KWD');
    document.getElementById('grandTotal').innerHTML = fmt(grand) + ' <span class="cart-totals__currency">KWD</span>';

    // Item count line under heading
    const itemCountEl = document.getElementById('cartItemCount');
    if (itemCountEl) {
      const word = itemCount === 1 ? 'piece' : 'pieces';
      itemCountEl.textContent = `${itemCount} ${word} · ${fmt(grand)} KWD`;
    }

    // Free shipping progress bar
    const remaining = FREE_SHIP_THRESHOLD - discounted;
    const fill = document.getElementById('shipFill');
    const msg = document.getElementById('shipMsg');
    if (fill && msg) {
      if (remaining <= 0) {
        fill.style.width = '100%';
        msg.innerHTML = `You\u2019ve unlocked <strong>complimentary delivery</strong> within Kuwait.`;
      } else {
        const pct = Math.max(8, Math.min(95, (discounted / FREE_SHIP_THRESHOLD) * 100));
        fill.style.width = pct + '%';
        msg.innerHTML = `Add <strong>${fmt(remaining)} KWD</strong> more for complimentary delivery.`;
      }
    }
  }

  // ── Quantity steppers
  document.querySelectorAll('.cart-item').forEach(item => {
    const input = item.querySelector('.qty__input');
    const minus = item.querySelector('[data-qty="minus"]');
    const plus = item.querySelector('[data-qty="plus"]');
    if (minus) minus.addEventListener('click', () => {
      input.value = Math.max(1, parseInt(input.value) - 1);
      recompute();
    });
    if (plus) plus.addEventListener('click', () => {
      input.value = Math.min(10, parseInt(input.value) + 1);
      recompute();
    });
    if (input) input.addEventListener('change', recompute);
  });

  // ── Remove with simple confirmation
  document.querySelectorAll('[data-action="remove"]').forEach(btn => {
    btn.addEventListener('click', () => {
      const item = btn.closest('.cart-item');
      if (!item) return;
      item.style.transition = 'opacity .3s ease, transform .3s ease';
      item.style.opacity = '0';
      item.style.transform = 'translateX(-12px)';
      setTimeout(() => {
        item.remove();
        // Check if cart is empty
        if (document.querySelectorAll('.cart-item').length === 0) {
          document.body.classList.add('cart-is-empty');
          const empty = document.getElementById('cartEmpty');
          if (empty) empty.hidden = false;
        } else {
          recompute();
        }
      }, 300);
    });
  });

  // ── Save for later (placeholder — mirrors remove for the demo)
  document.querySelectorAll('[data-action="wishlist"]').forEach(btn => {
    btn.addEventListener('click', () => {
      const original = btn.innerHTML;
      btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4"><polyline points="20 6 9 17 4 12"/></svg><span>Saved</span>';
      setTimeout(() => { btn.innerHTML = original; }, 1500);
    });
  });

  // ── Gift wrap
  const giftWrap = document.getElementById('giftWrap');
  if (giftWrap) giftWrap.addEventListener('change', recompute);

  // ── Order notes character counter
  const notes = document.getElementById('orderNotes');
  const notesCount = document.getElementById('notesCount');
  if (notes && notesCount) {
    notes.addEventListener('input', () => {
      notesCount.textContent = notes.value.length;
    });
  }

  // ── Discount validation (demo logic — valid codes: WELCOME10, SS26)
  window.applyDiscount = function() {
    const input = document.getElementById('discountInput');
    const msg = document.getElementById('discountMsg');
    const row = document.getElementById('discountRow');
    const code = (input.value || '').trim().toUpperCase();
    const amtEl = document.getElementById('discountAmt');
    const codeEl = document.getElementById('discountCode');

    if (!code) return;

    const validCodes = { 'WELCOME10': 10, 'SS26': 15, 'EID2026': 20 };
    if (validCodes[code] !== undefined) {
      const discount = validCodes[code];
      amtEl.dataset.amount = String(discount);
      amtEl.textContent = '−' + fmt(discount) + ' KWD';
      codeEl.textContent = code;
      row.hidden = false;
      msg.hidden = false;
      msg.className = 'cart-discount__msg is-success';
      msg.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" style="width:14px;height:14px"><polyline points="20 6 9 17 4 12"/></svg> ' + code + ' applied · ' + fmt(discount) + ' KWD off';
      input.value = '';
      recompute();
    } else {
      msg.hidden = false;
      msg.className = 'cart-discount__msg is-error';
      msg.textContent = `That code didn\u2019t work. Try WELCOME10 or SS26.`;
    }
  };

  // Initial computation
  recompute();
});

/* ============================================================================
   CHECKOUT PAGE — runs only on /checkout
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const co = document.getElementById('checkoutMain');
  if (!co) return;

  const SUBTOTAL = 186.5;
  let discountAmt = 0;
  let discountCode = '';

  function fmt(n) { return n.toFixed(3); }

  function getShippingCost() {
    const r = document.querySelector('input[name="shipping"]:checked');
    if (!r) return 0;
    switch (r.value) {
      case 'express': return 3.5;
      case 'same-day': return 5.0;
      case 'standard':
      case 'pickup':
      default: return 0;
    }
  }

  function getCodFee() {
    const r = document.querySelector('input[name="payment"]:checked');
    return r && r.value === 'cod' ? 1.5 : 0;
  }

  function recompute() {
    const discounted = Math.max(0, SUBTOTAL - discountAmt);
    const shipping = getShippingCost();
    const codFee = getCodFee();
    const total = discounted + shipping + codFee;

    document.getElementById('coSubtotal').textContent = fmt(SUBTOTAL) + ' KWD';
    document.getElementById('coShipping').textContent = shipping === 0 ? 'Free' : (fmt(shipping) + ' KWD');
    document.getElementById('coGrandTotal').innerHTML = fmt(total) + ' <span class="cart-totals__currency">KWD</span>';

    const placeStrong = document.getElementById('coPlaceTotal');
    if (placeStrong) placeStrong.textContent = fmt(total) + ' KWD';

    const toggleTotal = document.querySelector('.co-summary-toggle strong');
    if (toggleTotal) toggleTotal.textContent = fmt(total) + ' KWD';

    if (discountAmt > 0) {
      document.getElementById('coDiscountAmt').textContent = '−' + fmt(discountAmt) + ' KWD';
      document.getElementById('coDiscountCode').textContent = discountCode;
      document.getElementById('coDiscountRow').hidden = false;
    } else {
      document.getElementById('coDiscountRow').hidden = true;
    }
  }

  // Shipping method change
  document.querySelectorAll('input[name="shipping"]').forEach(r => {
    r.addEventListener('change', recompute);
  });

  // Payment method change
  document.querySelectorAll('input[name="payment"]').forEach(r => {
    r.addEventListener('change', recompute);
  });

  // Country change — show/hide Kuwait-specific fields
  const countrySelect = document.getElementById('country');
  const govField = document.getElementById('governorate');
  if (countrySelect && govField) {
    countrySelect.addEventListener('change', () => {
      const isKw = countrySelect.value === 'KW';
      const govLabel = govField.previousElementSibling;
      // We don't actually hide — different markets have similar admin divisions —
      // but in production this would swap options.
      if (govLabel) {
        govLabel.textContent = isKw ? 'Governorate' : 'Region / emirate';
      }
    });
  }

  // Card expiry MM/YY auto-format
  const cardExp = document.getElementById('cardExp');
  if (cardExp) {
    cardExp.addEventListener('input', () => {
      let v = cardExp.value.replace(/[^\d]/g, '').slice(0, 4);
      if (v.length >= 3) v = v.slice(0, 2) + ' / ' + v.slice(2);
      cardExp.value = v;
    });
  }

  // Card number — group every 4 digits
  const cardNum = document.getElementById('cardNumber');
  if (cardNum) {
    cardNum.addEventListener('input', () => {
      let v = cardNum.value.replace(/[^\d]/g, '').slice(0, 19);
      const groups = v.match(/.{1,4}/g);
      cardNum.value = groups ? groups.join(' ') : '';
    });
  }

  // Mobile summary toggle
  const toggle = document.getElementById('coSummaryToggle');
  const summaryWrap = document.querySelector('.co__summary');
  if (toggle && summaryWrap) {
    toggle.addEventListener('click', () => {
      const open = summaryWrap.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      toggle.querySelector('span:nth-child(1) span:nth-child(2)').textContent = open ? 'Hide order summary' : 'Show order summary';
    });
  }

  // Discount code
  window.coApplyDiscount = function() {
    const input = document.getElementById('coDiscountInput');
    const msg = document.getElementById('coDiscountMsg');
    const code = (input.value || '').trim().toUpperCase();
    if (!code) return;
    const valid = { 'WELCOME10': 10, 'SS26': 15, 'EID2026': 20 };
    if (valid[code] !== undefined) {
      discountAmt = valid[code];
      discountCode = code;
      msg.hidden = false;
      msg.className = 'cart-discount__msg is-success';
      msg.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" style="width:14px;height:14px"><polyline points="20 6 9 17 4 12"/></svg> ' + code + ' applied · ' + fmt(discountAmt) + ' KWD off';
      input.value = '';
      recompute();
    } else {
      msg.hidden = false;
      msg.className = 'cart-discount__msg is-error';
      msg.textContent = 'That code didn\u2019t work. Try WELCOME10 or SS26.';
    }
  };

  // Place order — basic validation + simulated processing
  const placeBtn = document.getElementById('placeOrderBtn');
  const form = document.getElementById('checkoutForm');
  if (form && placeBtn) {
    form.addEventListener('submit', (e) => {
      e.preventDefault();

      // Validation
      const required = form.querySelectorAll('input[required], select[required]');
      let firstInvalid = null;
      required.forEach(el => {
        if (!el.checkValidity()) {
          el.style.borderColor = '#c87a3a';
          if (!firstInvalid) firstInvalid = el;
        } else {
          el.style.borderColor = '';
        }
      });

      // Terms accept
      const terms = document.getElementById('termsAccept');
      if (terms && !terms.checked) {
        if (!firstInvalid) firstInvalid = terms;
      }

      if (firstInvalid) {
        firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
        firstInvalid.focus();
        return;
      }

      // Simulate processing
      placeBtn.disabled = true;
      const original = placeBtn.innerHTML;
      placeBtn.innerHTML = '<span>Processing securely…</span>';
      setTimeout(() => {
        placeBtn.innerHTML = '<span>✓ Order placed</span>';
        setTimeout(() => { window.location.href = '/checkout/confirmation'; }, 600);
      }, 1800);
    });
  }

  // Initial compute
  recompute();
});

/* ============================================================================
   AUTH PAGE — Login / Sign-up / Forgot Password
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  if (!document.querySelector('.auth')) return;

  const tabSignin = document.getElementById('tabSignin');
  const tabRegister = document.getElementById('tabRegister');
  const paneSignin = document.getElementById('paneSignin');
  const paneRegister = document.getElementById('paneRegister');
  const paneForgot = document.getElementById('paneForgot');
  const tabs = [tabSignin, tabRegister];
  const panes = [paneSignin, paneRegister, paneForgot];

  function showPane(targetPane, targetTab) {
    panes.forEach(p => {
      if (!p) return;
      p.hidden = (p !== targetPane);
      p.classList.toggle('is-active', p === targetPane);
    });
    tabs.forEach(t => {
      if (!t) return;
      const active = (t === targetTab);
      t.classList.toggle('is-active', active);
      t.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  // Tab clicks
  if (tabSignin) tabSignin.addEventListener('click', () => showPane(paneSignin, tabSignin));
  if (tabRegister) tabRegister.addEventListener('click', () => showPane(paneRegister, tabRegister));

  // "Sign in instead" link from register pane
  const goToSignin = document.getElementById('goToSignin');
  if (goToSignin) goToSignin.addEventListener('click', () => showPane(paneSignin, tabSignin));

  // Forgot password link
  const forgotLink = document.getElementById('forgotLink');
  if (forgotLink) forgotLink.addEventListener('click', (e) => {
    e.preventDefault();
    showPane(paneForgot, null);
  });
  const backFromForgot = document.getElementById('backFromForgot');
  if (backFromForgot) backFromForgot.addEventListener('click', () => showPane(paneSignin, tabSignin));

  // Password show/hide toggle
  document.querySelectorAll('.co-field__action').forEach(btn => {
    btn.addEventListener('click', () => {
      const input = btn.parentElement.querySelector('input');
      if (!input) return;
      const isPwd = input.type === 'password';
      input.type = isPwd ? 'text' : 'password';
      btn.classList.toggle('is-shown', isPwd);
    });
  });

  // Password strength meter
  const regPassword = document.getElementById('regPassword');
  const pwdStrength = document.getElementById('pwdStrength');
  if (regPassword && pwdStrength) {
    const label = pwdStrength.querySelector('.pwd-strength__label');
    regPassword.addEventListener('input', () => {
      const v = regPassword.value;
      let score = 0;
      if (v.length >= 8) score++;
      if (v.length >= 12) score++;
      if (/[A-Z]/.test(v) && /[a-z]/.test(v)) score++;
      if (/\d/.test(v)) score++;
      if (/[^A-Za-z0-9]/.test(v)) score++;
      pwdStrength.classList.remove('is-weak', 'is-medium', 'is-strong');
      if (v.length === 0) {
        if (label) label.textContent = 'Strength';
      } else if (score <= 2) {
        pwdStrength.classList.add('is-weak');
        if (label) label.textContent = 'Weak';
      } else if (score <= 3) {
        pwdStrength.classList.add('is-medium');
        if (label) label.textContent = 'Good';
      } else {
        pwdStrength.classList.add('is-strong');
        if (label) label.textContent = 'Strong';
      }
    });
  }

  // Sign-in handler (demo only — would call auth API in production)
  window.handleSignin = function() {
    const email = document.getElementById('signinEmail');
    const pwd = document.getElementById('signinPassword');
    if (!email.checkValidity() || !pwd.checkValidity()) {
      [email, pwd].forEach(el => {
        if (!el.checkValidity()) el.style.borderColor = '#c87a3a';
      });
      return;
    }
    // In production: POST /api/auth/signin then redirect to /account
    window.location.href = '/account';
  };

  window.handleRegister = function() {
    const required = document.querySelectorAll('#registerForm [required]');
    let firstInvalid = null;
    required.forEach(el => {
      if (!el.checkValidity()) {
        el.style.borderColor = '#c87a3a';
        if (!firstInvalid) firstInvalid = el;
      } else {
        el.style.borderColor = '';
      }
    });
    if (firstInvalid) { firstInvalid.focus(); return; }
    window.location.href = '/account';
  };

  window.handleForgot = function() {
    const email = document.getElementById('forgotEmail');
    if (!email.checkValidity()) { email.style.borderColor = '#c87a3a'; return; }
    const form = document.getElementById('forgotForm');
    const success = document.getElementById('forgotSuccess');
    const sentTo = document.getElementById('forgotSentTo');
    if (sentTo) sentTo.textContent = email.value;
    // Hide form fields, show success
    form.querySelectorAll('.co-field, button[type="submit"]').forEach(el => el.style.display = 'none');
    success.hidden = false;
  };
});

/* ============================================================================
   ADDRESSES PAGE — add-new form toggle
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const openBtn = document.getElementById('openAddressForm');
  const closeBtn = document.getElementById('closeAddressForm');
  const form = document.getElementById('addressForm');
  const card = document.getElementById('addAddressCard');
  if (!openBtn || !form) return;

  openBtn.addEventListener('click', () => {
    form.hidden = false;
    if (card) card.style.display = 'none';
    form.scrollIntoView({ behavior: 'smooth', block: 'center' });
    setTimeout(() => form.querySelector('input').focus(), 300);
  });
  if (closeBtn) closeBtn.addEventListener('click', () => {
    form.hidden = true;
    if (card) card.style.display = '';
  });
});

/* ============================================================================
   WISHLIST — remove + move-to-bag
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  if (!document.querySelector('.acct-wish-grid')) return;

  document.querySelectorAll('.wish-product__remove').forEach(btn => {
    btn.addEventListener('click', () => {
      const product = btn.closest('.wish-product');
      product.style.transition = 'opacity .3s ease, transform .3s ease';
      product.style.opacity = '0';
      product.style.transform = 'scale(0.96)';
      setTimeout(() => product.remove(), 300);
    });
  });

  document.querySelectorAll('.wish-product__add').forEach(btn => {
    btn.addEventListener('click', () => {
      const original = btn.textContent;
      btn.textContent = '✓ Added to bag';
      btn.disabled = true;
      setTimeout(() => {
        btn.textContent = original;
        btn.disabled = false;
      }, 1500);
    });
  });
});

/* ============================================================================
   ORDERS / RETURNS — filter chips switching
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  document.querySelectorAll('.acct-orders-toolbar__filters').forEach(group => {
    const chips = group.querySelectorAll('.filter-chip');
    chips.forEach(chip => {
      chip.addEventListener('click', () => {
        chips.forEach(c => {
          c.classList.remove('is-active');
          c.setAttribute('aria-selected', 'false');
        });
        chip.classList.add('is-active');
        chip.setAttribute('aria-selected', 'true');
      });
    });
  });
});

/* ============================================================================
   FAQ — accordion toggle
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  document.querySelectorAll('.faq-item__q').forEach(btn => {
    btn.addEventListener('click', () => {
      const expanded = btn.getAttribute('aria-expanded') === 'true';
      btn.setAttribute('aria-expanded', expanded ? 'false' : 'true');
    });
  });
});

/* ============================================================================
   POLICY TOC — scroll-spy: highlight the active section
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const tocLinks = document.querySelectorAll('.policy__toc-list a');
  if (!tocLinks.length) return;
  const sections = Array.from(document.querySelectorAll('.policy-section')).filter(s => s.id);
  if (!sections.length) return;

  // Smooth scroll on TOC clicks
  tocLinks.forEach(link => {
    link.addEventListener('click', (e) => {
      const id = link.getAttribute('href').slice(1);
      const target = document.getElementById(id);
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        history.replaceState(null, '', '#' + id);
      }
    });
  });

  // Scroll-spy
  const setActive = (id) => {
    tocLinks.forEach(a => {
      const match = a.getAttribute('href') === '#' + id;
      a.classList.toggle('is-active', match);
    });
  };
  const io = new IntersectionObserver(entries => {
    // Find the section with the most visible space at the top
    const visible = entries.filter(e => e.isIntersecting);
    if (visible.length) {
      visible.sort((a, b) => a.target.getBoundingClientRect().top - b.target.getBoundingClientRect().top);
      setActive(visible[0].target.id);
    }
  }, { rootMargin: '-100px 0px -60% 0px' });
  sections.forEach(s => io.observe(s));
});

/* ============================================================================
   CONTACT — form submission
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  if (!document.getElementById('contactForm')) return;

  window.handleContact = function() {
    const form = document.getElementById('contactForm');
    const required = form.querySelectorAll('[required]');
    let firstInvalid = null;
    required.forEach(el => {
      if (!el.checkValidity()) {
        el.style.borderColor = '#c87a3a';
        if (!firstInvalid) firstInvalid = el;
      } else {
        el.style.borderColor = '';
      }
    });
    if (firstInvalid) { firstInvalid.focus(); return; }

    // Show success, hide form
    const name = document.getElementById('contactName').value;
    const success = document.getElementById('contactSuccess');
    const sentName = document.getElementById('contactSentName');
    if (sentName) sentName.textContent = name;
    form.querySelectorAll('.co-field, .co-field-row, .auth-form__consent, button[type="submit"]').forEach(el => el.style.display = 'none');
    success.hidden = false;
  };
});

/* ============================================================================
   SPRINT 5 — GLOBAL OVERLAYS & UTILITY PAGES
   Mini-cart drawer · Search overlay · Cookie banner · Track lookup · Maintenance
============================================================================ */

/* ──────────────────────────────────────────────────────────
   BODY SCROLL LOCK — shared helper
   iOS Safari-safe via position:fixed + saved scrollY pattern
   ────────────────────────────────────────────────────────── */
let _savedScrollY = 0;
let _lockCount = 0;
function lockBodyScroll() {
  if (_lockCount === 0) {
    _savedScrollY = window.scrollY || window.pageYOffset || 0;
    document.body.style.position = 'fixed';
    document.body.style.top = `-${_savedScrollY}px`;
    document.body.style.left = '0';
    document.body.style.right = '0';
    document.body.style.width = '100%';
  }
  _lockCount++;
}
function unlockBodyScroll() {
  _lockCount = Math.max(0, _lockCount - 1);
  if (_lockCount === 0) {
    document.body.style.position = '';
    document.body.style.top = '';
    document.body.style.left = '';
    document.body.style.right = '';
    document.body.style.width = '';
    window.scrollTo(0, _savedScrollY);
  }
}

/* ──────────────────────────────────────────────────────────
   MINI-CART DRAWER
   ────────────────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', function() {
  const drawer = document.getElementById('miniCart');
  if (!drawer) return;

  const trigger = document.getElementById('hdrCartBtn');
  const backdrop = document.getElementById('miniCartBackdrop');
  const closeBtn = document.getElementById('miniCartClose');
  const itemsList = document.getElementById('miniCartItems');
  const emptyState = document.getElementById('miniCartEmpty');
  const countEl = document.getElementById('miniCartCount');
  const progress = document.getElementById('mcProgress');
  const foot = drawer.querySelector('.mc-foot');

  function openCart() {
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    lockBodyScroll();
  }
  function closeCart() {
    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    unlockBodyScroll();
  }

  if (trigger) trigger.addEventListener('click', (e) => { e.preventDefault(); openCart(); });
  if (backdrop) backdrop.addEventListener('click', closeCart);
  if (closeBtn) closeBtn.addEventListener('click', closeCart);

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && drawer.classList.contains('is-open')) closeCart();
  });

  // Update cart count + show/hide empty state
  function refreshCart() {
    const items = itemsList ? itemsList.querySelectorAll('.mc-item') : [];
    const count = items.length;
    if (countEl) countEl.textContent = count;
    // Update header badge too
    const headerBadge = document.querySelector('.hdr__cart-count');
    if (headerBadge) headerBadge.textContent = count;
    if (count === 0) {
      if (itemsList) itemsList.hidden = true;
      if (progress) progress.hidden = true;
      if (foot) foot.hidden = true;
      if (emptyState) emptyState.hidden = false;
    } else {
      if (itemsList) itemsList.hidden = false;
      if (progress) progress.hidden = false;
      if (foot) foot.hidden = false;
      if (emptyState) emptyState.hidden = true;
    }
    // Recompute subtotal
    let subtotal = 0;
    items.forEach(li => {
      const priceText = li.querySelector('.mc-item__price').textContent;
      const price = parseFloat(priceText.replace(/[^\d.]/g, '')) || 0;
      const qty = parseInt(li.querySelector('.qty-stepper__value').textContent, 10) || 1;
      subtotal += price * qty;
    });
    const totalEl = drawer.querySelector('.mc-foot__totals strong');
    if (totalEl) totalEl.innerHTML = subtotal.toFixed(3) + ' <em>KWD</em>';
  }

  // Wire qty steppers
  drawer.querySelectorAll('.qty-stepper--sm').forEach(stepper => {
    const valueEl = stepper.querySelector('.qty-stepper__value');
    const [minus, plus] = stepper.querySelectorAll('.qty-stepper__btn');
    if (minus) minus.addEventListener('click', () => {
      const v = parseInt(valueEl.textContent, 10) || 1;
      if (v > 1) { valueEl.textContent = v - 1; refreshCart(); }
    });
    if (plus) plus.addEventListener('click', () => {
      const v = parseInt(valueEl.textContent, 10) || 1;
      if (v < 10) { valueEl.textContent = v + 1; refreshCart(); }
    });
  });

  // Wire remove buttons
  drawer.querySelectorAll('.mc-item__remove').forEach(btn => {
    btn.addEventListener('click', () => {
      const li = btn.closest('.mc-item');
      li.style.transition = 'opacity .25s ease, transform .25s ease';
      li.style.opacity = '0';
      li.style.transform = 'translateX(20px)';
      setTimeout(() => { li.remove(); refreshCart(); }, 250);
    });
  });
});

/* ──────────────────────────────────────────────────────────
   SEARCH OVERLAY
   ────────────────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', function() {
  const overlay = document.getElementById('searchOverlay');
  if (!overlay) return;

  const trigger = document.getElementById('hdrSearchBtn');
  const backdrop = document.getElementById('searchOverlayBackdrop');
  const closeBtn = document.getElementById('searchOverlayClose');
  const input = document.getElementById('searchOverlayInput');
  const clearBtn = document.getElementById('searchOverlayClear');
  const idleEl = document.getElementById('searchOverlayIdle');
  const resultsEl = document.getElementById('searchOverlayResults');
  const noResultsEl = document.getElementById('searchOverlayNoResults');
  const viewAllLink = document.getElementById('searchOverlayAll');

  function openSearch() {
    overlay.classList.add('is-open');
    overlay.setAttribute('aria-hidden', 'false');
    lockBodyScroll();
    setTimeout(() => { if (input) input.focus(); }, 100);
  }
  function closeSearch() {
    overlay.classList.remove('is-open');
    overlay.setAttribute('aria-hidden', 'true');
    unlockBodyScroll();
  }

  if (trigger) trigger.addEventListener('click', (e) => { e.preventDefault(); openSearch(); });
  if (backdrop) backdrop.addEventListener('click', closeSearch);
  if (closeBtn) closeBtn.addEventListener('click', closeSearch);

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && overlay.classList.contains('is-open')) closeSearch();
  });

  // Live state management
  function setState(state) {
    if (idleEl) idleEl.hidden = (state !== 'idle');
    if (resultsEl) resultsEl.hidden = (state !== 'results');
    if (noResultsEl) noResultsEl.hidden = (state !== 'noresults');
  }

  // Live suggestions: debounced fetch of a real partial from /search/suggest, with the previous
  // in-flight request aborted on each keystroke so results never arrive out of order.
  let searchAbort = null;
  let searchTimer = null;

  function fetchSuggest(q) {
    if (searchAbort) searchAbort.abort();
    searchAbort = new AbortController();
    fetch(`/search/suggest?q=${encodeURIComponent(q)}`, {
      signal: searchAbort.signal,
      headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
      .then(r => r.ok ? r.text() : Promise.reject(new Error('http ' + r.status)))
      .then(html => {
        if (resultsEl) resultsEl.innerHTML = html;
        const countEl = resultsEl ? resultsEl.querySelector('[data-result-count]') : null;
        const count = countEl ? parseInt(countEl.getAttribute('data-result-count') || '0', 10) : 0;
        setState(count > 0 ? 'results' : 'noresults');
      })
      .catch(err => {
        if (err && err.name === 'AbortError') return; // superseded by a newer keystroke
        setState('noresults');
      });
  }

  if (input) {
    input.addEventListener('input', () => {
      const q = input.value.trim();
      if (clearBtn) clearBtn.hidden = !q;
      if (viewAllLink) viewAllLink.href = q ? `/search?q=${encodeURIComponent(q)}` : '/search';
      if (searchTimer) clearTimeout(searchTimer);
      if (q.length < 2) {
        if (searchAbort) searchAbort.abort();
        setState('idle');
        return;
      }
      searchTimer = setTimeout(() => fetchSuggest(q), 220);
    });

    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        const q = input.value.trim();
        if (q) window.location.href = `/search?q=${encodeURIComponent(q)}`;
      }
    });
  }

  if (clearBtn) clearBtn.addEventListener('click', () => {
    if (input) { input.value = ''; input.focus(); }
    if (clearBtn) clearBtn.hidden = true;
    setState('idle');
  });

  // Clicking a popular search chip or recent item fills the input
  overlay.querySelectorAll('.so-chips button, .so-recent button').forEach(btn => {
    btn.addEventListener('click', () => {
      const term = btn.textContent.trim();
      if (input) {
        input.value = term;
        input.dispatchEvent(new Event('input'));
        input.focus();
      }
    });
  });
});

/* ──────────────────────────────────────────────────────────
   COOKIE CONSENT BANNER
   ────────────────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', function() {
  const banner = document.getElementById('cookieBanner');
  const modal = document.getElementById('cookieModal');
  const reopen = document.getElementById('cookieReopen');
  if (!banner || !modal) return;

  const STORAGE_KEY = 'ws_cookie_consent';

  function readConsent() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) { return null; }
  }
  function writeConsent(consent) {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        ...consent,
        timestamp: Date.now(),
      }));
    } catch (e) { /* localStorage may be disabled */ }
  }

  function showBanner() {
    banner.hidden = false;
    requestAnimationFrame(() => banner.classList.add('is-visible'));
  }
  function hideBanner() {
    banner.classList.remove('is-visible');
    setTimeout(() => { banner.hidden = true; }, 400);
  }
  function showReopen() {
    if (reopen) reopen.hidden = false;
  }

  function openModal(prefill) {
    modal.classList.add('is-open');
    modal.setAttribute('aria-hidden', 'false');
    if (prefill) {
      const toggles = modal.querySelectorAll('input[data-cookie]');
      toggles.forEach(t => {
        const key = t.getAttribute('data-cookie');
        t.checked = !!prefill[key];
      });
    }
  }
  function closeModal() {
    modal.classList.remove('is-open');
    modal.setAttribute('aria-hidden', 'true');
  }

  // On load: decide banner vs reopen vs nothing
  const existing = readConsent();
  if (!existing) {
    setTimeout(showBanner, 800);
  } else {
    showReopen();
  }

  // Accept all
  const acceptBtn = document.getElementById('cookieAccept');
  if (acceptBtn) acceptBtn.addEventListener('click', () => {
    writeConsent({ functional: true, analytics: true, marketing: true });
    hideBanner();
    showReopen();
  });

  // Reject all (banner)
  const rejectBtn = document.getElementById('cookieReject');
  if (rejectBtn) rejectBtn.addEventListener('click', () => {
    writeConsent({ functional: false, analytics: false, marketing: false });
    hideBanner();
    showReopen();
  });

  // Customize - open modal
  const customizeBtn = document.getElementById('cookieCustomize');
  if (customizeBtn) customizeBtn.addEventListener('click', () => {
    openModal(existing || { functional: true, analytics: false, marketing: false });
  });

  // Modal close + backdrop
  const modalClose = document.getElementById('cookieModalClose');
  const modalBackdrop = document.getElementById('cookieModalBackdrop');
  if (modalClose) modalClose.addEventListener('click', closeModal);
  if (modalBackdrop) modalBackdrop.addEventListener('click', closeModal);

  // Modal: Reject all
  const modalReject = document.getElementById('cookieModalReject');
  if (modalReject) modalReject.addEventListener('click', () => {
    writeConsent({ functional: false, analytics: false, marketing: false });
    closeModal();
    hideBanner();
    showReopen();
  });

  // Modal: Save preferences
  const modalSave = document.getElementById('cookieModalSave');
  if (modalSave) modalSave.addEventListener('click', () => {
    const consent = { functional: false, analytics: false, marketing: false };
    modal.querySelectorAll('input[data-cookie]').forEach(t => {
      consent[t.getAttribute('data-cookie')] = t.checked;
    });
    writeConsent(consent);
    closeModal();
    hideBanner();
    showReopen();
  });

  // Re-open button
  if (reopen) reopen.addEventListener('click', () => {
    openModal(readConsent() || { functional: true, analytics: false, marketing: false });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal.classList.contains('is-open')) closeModal();
  });
});

/* ──────────────────────────────────────────────────────────
   TRACK PAGE — order lookup
   ────────────────────────────────────────────────────────── */
window.handleTrackLookup = function() {
  const numberEl = document.getElementById('trackNumber');
  const emailEl = document.getElementById('trackEmail');
  const formWrap = document.getElementById('trackForm');
  const errorEl = document.getElementById('trackError');
  const resultEl = document.getElementById('trackResult');
  if (!numberEl || !emailEl) return;

  // Light validation
  let ok = true;
  [numberEl, emailEl].forEach(el => {
    if (!el.checkValidity()) { el.style.borderColor = '#c87a3a'; ok = false; }
    else { el.style.borderColor = ''; }
  });
  if (!ok) return;

  // Demo logic: order numbers containing "WS" succeed, anything else errors
  const orderNum = numberEl.value.trim().toUpperCase();
  const success = orderNum.includes('WS');

  if (success) {
    if (formWrap) formWrap.hidden = true;
    if (resultEl) resultEl.hidden = false;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  } else {
    // Show error state inside form wrap
    const form = formWrap ? formWrap.querySelector('.track__form') : null;
    if (form) form.style.display = 'none';
    if (errorEl) errorEl.hidden = false;
  }
};

document.addEventListener('DOMContentLoaded', function() {
  const errBack = document.getElementById('trackErrorBack');
  const lookupAgain = document.getElementById('trackLookupAgain');
  const formWrap = document.getElementById('trackForm');
  const errorEl = document.getElementById('trackError');
  const resultEl = document.getElementById('trackResult');

  function resetLookup() {
    if (formWrap) formWrap.hidden = false;
    const form = formWrap ? formWrap.querySelector('.track__form') : null;
    if (form) form.style.display = '';
    if (errorEl) errorEl.hidden = true;
    if (resultEl) resultEl.hidden = true;
    const numberEl = document.getElementById('trackNumber');
    const emailEl = document.getElementById('trackEmail');
    if (numberEl) numberEl.value = '';
    if (emailEl) emailEl.value = '';
  }

  if (errBack) errBack.addEventListener('click', resetLookup);
  if (lookupAgain) lookupAgain.addEventListener('click', resetLookup);
});

/* ──────────────────────────────────────────────────────────
   MAINTENANCE PAGE — notify-me email capture
   ────────────────────────────────────────────────────────── */
window.handleMaintNotify = function() {
  const emailEl = document.getElementById('maintEmail');
  const success = document.getElementById('maintSuccess');
  const form = document.querySelector('.maint__form');
  if (!emailEl) return;
  if (!emailEl.checkValidity()) {
    emailEl.style.borderColor = '#c87a3a';
    return;
  }
  if (form) form.querySelector('.maint__form-row').style.display = 'none';
  if (form) {
    const eyebrow = form.querySelector('.eyebrow');
    if (eyebrow) eyebrow.style.display = 'none';
  }
  if (success) success.hidden = false;
};

/* ============================================================================
   HERO VIDEO — pause if user prefers reduced motion
============================================================================ */
document.addEventListener('DOMContentLoaded', function() {
  const heroVideo = document.querySelector('.hero__video');
  if (!heroVideo) return;
  // Respect reduced-motion preference
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    heroVideo.pause();
    heroVideo.removeAttribute('autoplay');
  }
  // Pause when off-screen to save battery
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          heroVideo.play().catch(() => {}); // ignore autoplay-blocked errors
        } else {
          heroVideo.pause();
        }
      });
    }, { threshold: 0.1 });
    observer.observe(heroVideo);
  }
});

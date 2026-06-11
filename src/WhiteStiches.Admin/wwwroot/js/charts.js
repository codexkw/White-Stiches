/* ============================================================================
   WHITE STITCHES ADMIN — Tiny SVG charts (Phase 1E‑1)
   Dependency-free. Renders the dashboard revenue/orders time series as an
   area+line chart with order bars, reading its data from a JSON <script> block.
   Usage:
     <div class="ws-chart" data-series="#ws-series"></div>
     <script type="application/json" id="ws-series">[{ "d":"06-01","r":120.5,"o":3 }, ...]</script>
============================================================================ */
(function () {
  'use strict';

  var NS = 'http://www.w3.org/2000/svg';

  function el(name, attrs) {
    var node = document.createElementNS(NS, name);
    for (var k in attrs) { if (attrs.hasOwnProperty(k)) node.setAttribute(k, attrs[k]); }
    return node;
  }

  function fmt(n) {
    return Number(n).toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  }

  function renderChart(container) {
    var sel = container.getAttribute('data-series');
    var src = sel ? document.querySelector(sel) : null;
    if (!src) return;

    var data;
    try { data = JSON.parse(src.textContent || '[]'); } catch (e) { return; }
    if (!data.length) {
      container.innerHTML = '<p class="ws-chart__empty">No data in this period.</p>';
      return;
    }

    var W = container.clientWidth || 760;
    var H = 260;
    var padL = 48, padR = 16, padT = 16, padB = 28;
    var plotW = W - padL - padR;
    var plotH = H - padT - padB;

    var maxRev = Math.max.apply(null, data.map(function (p) { return p.r; })).valueOf() || 1;
    var maxOrders = Math.max.apply(null, data.map(function (p) { return p.o; })).valueOf() || 1;
    var n = data.length;

    var x = function (i) { return padL + (n === 1 ? plotW / 2 : (i / (n - 1)) * plotW); };
    var yRev = function (v) { return padT + plotH - (v / maxRev) * plotH; };

    var svg = el('svg', { viewBox: '0 0 ' + W + ' ' + H, width: '100%', height: H, class: 'ws-chart__svg' });

    // Horizontal gridlines + y labels (revenue)
    var ticks = 4;
    for (var t = 0; t <= ticks; t++) {
      var gy = padT + (plotH / ticks) * t;
      svg.appendChild(el('line', { x1: padL, y1: gy, x2: W - padR, y2: gy, class: 'ws-chart__grid' }));
      var label = el('text', { x: padL - 8, y: gy + 4, class: 'ws-chart__axis', 'text-anchor': 'end' });
      label.textContent = fmt(maxRev * (1 - t / ticks));
      svg.appendChild(label);
    }

    // Order bars (secondary, faint)
    var barW = Math.max(2, (plotW / n) * 0.45);
    data.forEach(function (p, i) {
      var bh = (p.o / maxOrders) * plotH;
      svg.appendChild(el('rect', {
        x: x(i) - barW / 2, y: padT + plotH - bh, width: barW, height: bh,
        rx: 1.5, class: 'ws-chart__bar'
      }));
    });

    // Revenue area + line
    var linePts = data.map(function (p, i) { return x(i) + ',' + yRev(p.r); });
    var areaPath = 'M' + padL + ',' + (padT + plotH) + ' L' + linePts.join(' L') +
      ' L' + x(n - 1) + ',' + (padT + plotH) + ' Z';
    svg.appendChild(el('path', { d: areaPath, class: 'ws-chart__area' }));
    svg.appendChild(el('polyline', { points: linePts.join(' '), class: 'ws-chart__line' }));

    // Points + value labels on hover (title tooltip)
    data.forEach(function (p, i) {
      var c = el('circle', { cx: x(i), cy: yRev(p.r), r: 3, class: 'ws-chart__dot' });
      var title = el('title', {});
      title.textContent = p.d + ' · ' + fmt(p.r) + ' KWD · ' + p.o + ' orders';
      c.appendChild(title);
      svg.appendChild(c);
    });

    // X labels (sparse: ~6 across)
    var step = Math.max(1, Math.ceil(n / 6));
    data.forEach(function (p, i) {
      if (i % step !== 0 && i !== n - 1) return;
      var lbl = el('text', { x: x(i), y: H - 8, class: 'ws-chart__axis', 'text-anchor': 'middle' });
      lbl.textContent = p.d;
      svg.appendChild(lbl);
    });

    container.innerHTML = '';
    container.appendChild(svg);
  }

  function initAll() {
    document.querySelectorAll('.ws-chart[data-series]').forEach(renderChart);
  }

  document.addEventListener('DOMContentLoaded', initAll);
  var resizeTimer;
  window.addEventListener('resize', function () {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(initAll, 200);
  });
  window.WSCharts = { initAll: initAll };
})();

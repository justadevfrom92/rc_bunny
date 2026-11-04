/*
  Styled calendar for every date field (input[type=date]) in the admin and on the website.
  The browser's own date popup can't be styled, so the field becomes a text box that opens
  this calendar. It still submits YYYY-MM-DD, and min / max on the field are respected.
  Keyboard: arrows move a day/week, PageUp/PageDown a month, Enter picks, Escape closes.
*/
(function () {
  'use strict';
  if (window.__datePicker) return; window.__datePicker = true;
  var MONTHS = ['January','February','March','April','May','June','July','August','September','October','November','December'];
  var DAYS = ['Su','Mo','Tu','We','Th','Fr','Sa'];
  var CAL = '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/></svg>';

  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function iso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function parse(s) { var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || ''); return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null; }

  function css() {
    if (document.getElementById('dp-css')) return;
    var s = document.createElement('style'); s.id = 'dp-css';
    s.textContent =
      '.dp-wrap{position:relative;display:block;width:100%}.dp-wrap>input{width:100%;padding-right:40px!important}' +
      '.dp-btn{position:absolute;right:6px;top:50%;transform:translateY(-50%);width:32px;height:32px;display:inline-flex;align-items:center;justify-content:center;border:0;border-radius:6px;background:transparent;color:#5b6b80;cursor:pointer;padding:0}' +
      '.dp-btn:hover{background:rgba(16,34,71,.07);color:#102247}.dp-btn::after,.dp-btn::before{display:none!important}' +
      '.dp{position:absolute;z-index:9999;width:292px;background:#fff;border:1px solid #d9e1ec;border-radius:12px;box-shadow:0 14px 34px rgba(16,34,71,.22);padding:12px;font:14px/1.2 system-ui,-apple-system,Segoe UI,sans-serif;color:#102247}' +
      '.dp-head{display:flex;align-items:center;justify-content:space-between;margin-bottom:8px}.dp-head b{font-size:15px}' +
      '.dp-nav{width:32px;height:32px;border:1px solid #d9e1ec;border-radius:8px;background:#fff;cursor:pointer;font-size:18px;line-height:1;color:#102247;padding:0}.dp-nav:hover{background:#eef4fb}' +
      '.dp-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:2px}.dp-dow{font-size:11px;font-weight:700;color:#7a879a;text-align:center;padding:4px 0;text-transform:uppercase}' +
      '.dp-day{height:36px;border:0;border-radius:8px;background:transparent;cursor:pointer;font:inherit;color:#102247;padding:0}' +
      '.dp-day:hover{background:#e6f6fd}.dp-day.out{color:#b4bfcd}.dp-day.today{box-shadow:inset 0 0 0 1px #00aeef;font-weight:700}' +
      '.dp-day.sel{background:#00aeef;color:#fff;font-weight:700}.dp-day.focus{outline:2px solid #102247;outline-offset:-2px}.dp-day:disabled{color:#d3d9e2;cursor:not-allowed;background:transparent}' +
      '.dp-foot{display:flex;justify-content:space-between;margin-top:10px;border-top:1px solid #eef1f5;padding-top:8px}' +
      '.dp-link{border:0;background:none;color:#0093cc;font:600 13px system-ui,sans-serif;cursor:pointer;padding:4px 6px;border-radius:6px}.dp-link:hover{background:#eef4fb}';
    document.head.appendChild(s);
  }

  var open = null;
  function close() { if (open) { open.el.remove(); document.removeEventListener('mousedown', outside, true); open = null; } }
  function outside(e) { if (open && !open.el.contains(e.target) && !open.wrap.contains(e.target)) close(); }

  function show(input, wrap) {
    close();
    var sel = parse(input.value), min = parse(input.dataset.min), max = parse(input.dataset.max);
    var view = sel ? new Date(sel) : new Date(); view.setDate(1);
    var focus = sel ? new Date(sel) : new Date();
    var el = document.createElement('div'); el.className = 'dp'; el.setAttribute('role', 'dialog'); el.setAttribute('aria-label', 'Choose a date');
    document.body.appendChild(el);
    open = { el: el, wrap: wrap };
    function allowed(d) { return !(min && d < min) && !(max && d > max); }
    function pick(d) { if (!allowed(d)) return; input.value = iso(d); input.dispatchEvent(new Event('input', { bubbles: true })); input.dispatchEvent(new Event('change', { bubbles: true })); close(); input.focus(); }
    function draw() {
      var y = view.getFullYear(), m = view.getMonth(), start = new Date(y, m, 1 - new Date(y, m, 1).getDay()), today = iso(new Date());
      var h = '<div class="dp-head"><button type="button" class="dp-nav" data-m="-1" aria-label="Previous month">‹</button><b>' + MONTHS[m] + ' ' + y + '</b><button type="button" class="dp-nav" data-m="1" aria-label="Next month">›</button></div><div class="dp-grid">';
      DAYS.forEach(function (d) { h += '<span class="dp-dow">' + d + '</span>'; });
      for (var i = 0; i < 42; i++) {
        var d = new Date(start); d.setDate(start.getDate() + i); var k = iso(d);
        h += '<button type="button" class="dp-day' + (d.getMonth() !== m ? ' out' : '') + (k === today ? ' today' : '') + (sel && k === iso(sel) ? ' sel' : '') + (k === iso(focus) ? ' focus' : '') +
          '" data-d="' + k + '"' + (allowed(d) ? '' : ' disabled') + ' aria-label="' + d.toDateString() + '">' + d.getDate() + '</button>';
      }
      h += '</div><div class="dp-foot"><button type="button" class="dp-link" data-today>Today</button><button type="button" class="dp-link" data-clear>Clear</button></div>';
      el.innerHTML = h;
      var r = input.getBoundingClientRect(), top = window.scrollY + r.bottom + 6, left = window.scrollX + r.left;
      if (left + 292 > window.scrollX + document.documentElement.clientWidth - 8) left = window.scrollX + document.documentElement.clientWidth - 300;
      if (r.bottom + el.offsetHeight + 8 > window.innerHeight && r.top > el.offsetHeight + 8) top = window.scrollY + r.top - el.offsetHeight - 6;
      el.style.top = top + 'px'; el.style.left = Math.max(8, left) + 'px';
    }
    el.addEventListener('click', function (e) {
      var b = e.target.closest('button'); if (!b) return;
      if (b.dataset.m) { view.setMonth(view.getMonth() + +b.dataset.m); focus = new Date(view); draw(); }
      else if (b.dataset.d) pick(parse(b.dataset.d));
      else if (b.hasAttribute('data-today')) pick(new Date());
      else if (b.hasAttribute('data-clear')) { input.value = ''; input.dispatchEvent(new Event('change', { bubbles: true })); close(); input.focus(); }
    });
    el.addEventListener('keydown', keys); input.addEventListener('keydown', keys);
    function keys(e) {
      if (!open || open.el !== el) return;
      var step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
      if (step) { e.preventDefault(); focus.setDate(focus.getDate() + step); }
      else if (e.key === 'PageUp' || e.key === 'PageDown') { e.preventDefault(); focus.setMonth(focus.getMonth() + (e.key === 'PageUp' ? -1 : 1)); }
      else if (e.key === 'Enter') { e.preventDefault(); pick(focus); return; }
      else if (e.key === 'Escape') { close(); return; }
      else return;
      view = new Date(focus); view.setDate(1); draw();
    }
    draw();
    setTimeout(function () { document.addEventListener('mousedown', outside, true); }, 0);
  }

  function enhance(input) {
    if (input.dataset.dp) return; input.dataset.dp = '1';
    if (input.min) input.dataset.min = input.min; if (input.max) input.dataset.max = input.max;
    input.type = 'text'; input.inputMode = 'numeric'; input.autocomplete = 'off'; input.classList.add('dp-input');
    if (!input.placeholder) input.placeholder = 'YYYY-MM-DD';
    input.pattern = '\\d{4}-\\d{2}-\\d{2}';
    var wrap = document.createElement('span'); wrap.className = 'dp-wrap';
    if (input.style.maxWidth) { wrap.style.maxWidth = input.style.maxWidth; input.style.maxWidth = ''; }
    input.parentNode.insertBefore(wrap, input); wrap.appendChild(input);
    var b = document.createElement('button'); b.type = 'button'; b.className = 'dp-btn'; b.innerHTML = CAL; b.setAttribute('aria-label', 'Open calendar');
    wrap.appendChild(b);
    b.addEventListener('click', function () { open && open.wrap === wrap ? close() : show(input, wrap); });
    input.addEventListener('click', function () { if (!open || open.wrap !== wrap) show(input, wrap); });
  }

  function scan(root) { (root || document).querySelectorAll('input[type=date]').forEach(enhance); }
  function start() {
    css(); scan();
    new MutationObserver(function (list) { list.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1 && !(open && open.el.contains(n))) { if (n.matches && n.matches('input[type=date]')) enhance(n); else scan(n); } }); }); })
      .observe(document.body, { childList: true, subtree: true });
    window.addEventListener('resize', close);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();

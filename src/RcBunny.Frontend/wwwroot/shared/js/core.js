/*
  Shared website helpers: page-ready, catalog access, pricing and navigation.
  ET.defaults is filled by /shared/config/catalog.js, which Laravel builds from the
  database, so anything changed in the admin shows up here on the next page load.
*/
(function () {
  'use strict';
  var ET = window.ET;

  ET.ready = function (fn) {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', fn);
    else fn();
  };

  ET.esc = function (s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  };

  /* ---------- Data ---------- */
  ET.data = function (key) { return JSON.parse(JSON.stringify(ET.defaults[key])); };

  /* ---------- Catalog helpers ---------- */
  function find(list, k, v) { return list.filter(function (x) { return x[k] === v; })[0]; }
  ET.plan = function (code) { return find(ET.data('PLANS'), 'internal', code); };
  ET.group = function (slug) {
    var g = find(ET.data('PLAN_GROUPS'), 'slug', slug);
    if (!g) return [];
    return g.plans.map(ET.plan).filter(function (p) { return p && p.active; });
  };
  ET.marketForZip = function (zip) {
    var z = parseInt(zip, 10);
    var r = ET.data('ZIP_RANGES').filter(function (x) { return z >= x[0] && z <= x[1]; })[0];
    return r ? find(ET.data('MARKETS'), 'name', r[2]) : null;
  };
  ET.fee = function (market) { return find(ET.data('TDSP_FEES'), 'market', market); };
  ET.rate = function (code, market) {
    return ET.data('RATES').filter(function (r) { return r.plan === code && r.market === market; })[0];
  };
  // Average price in ¢/kWh at a usage level, the way the EFL shows it.
  ET.price = function (code, market, kwh) {
    var p = ET.plan(code), r = ET.rate(code, market), f = ET.fee(market);
    if (!p || !r || !f) return null;
    return r.energy + f.perKwh + ((f.perBill + p.mrc) / kwh) * 100;
  };

  /* ---------- Navigation ---------- */
  ET.query = function () { return new URLSearchParams(location.search); };
  ET.page = function () { return location.pathname.split('/').pop() || 'index.html'; };
  ET.go = function (url, newTab) {
    if (newTab && window.open(url, '_blank')) return;
    location.href = url;
  };
})();

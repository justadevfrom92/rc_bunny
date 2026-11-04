/*
  Show / hide password: every password field (admin and website) gets an eye button
  inside it on the right. Clicking it shows the password; clicking again hides it.
  Fields added to the page later are picked up too.
*/
(function () {
  'use strict';
  if (window.__pwToggle) return; window.__pwToggle = true;

  var EYE = '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>';
  var EYE_OFF = '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 3l18 18M10.6 5.1A10.6 10.6 0 0 1 12 5c6.5 0 10 7 10 7a17.6 17.6 0 0 1-3.2 4.2M6.6 6.6C3.8 8.4 2 12 2 12s3.5 7 10 7c1.7 0 3.2-.4 4.5-1.1M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>';

  function style() {
    if (document.getElementById('pw-toggle-css')) return;
    var s = document.createElement('style'); s.id = 'pw-toggle-css';
    s.textContent = '.pw-wrap{position:relative;display:block;width:100%}.pw-wrap>input{width:100%;padding-right:44px!important}' +
      '.pw-eye{position:absolute;right:6px;top:50%;transform:translateY(-50%);display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;padding:0;margin:0;border:0;border-radius:6px;background:transparent;color:#5b6b80;cursor:pointer}' +
      '.pw-eye:hover,.pw-eye:focus-visible{color:#102247;background:rgba(16,34,71,.07)}.pw-eye::after,.pw-eye::before{display:none!important}';
    document.head.appendChild(s);
  }

  function add(input) {
    if (input.dataset.pwToggle) return; input.dataset.pwToggle = '1';
    var wrap = document.createElement('span'); wrap.className = 'pw-wrap';
    input.parentNode.insertBefore(wrap, input); wrap.appendChild(input);
    var b = document.createElement('button');
    b.type = 'button'; b.className = 'pw-eye'; b.innerHTML = EYE;
    b.setAttribute('aria-label', 'Show password'); b.setAttribute('aria-pressed', 'false');
    b.addEventListener('click', function () {
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      b.innerHTML = show ? EYE_OFF : EYE;
      b.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
      b.setAttribute('aria-pressed', show ? 'true' : 'false');
      input.focus();
    });
    wrap.appendChild(b);
  }

  function scan(root) { (root || document).querySelectorAll('input[type=password]').forEach(add); }

  function start() {
    style(); scan();
    new MutationObserver(function (list) { list.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1) { if (n.matches && n.matches('input[type=password]')) add(n); else scan(n); } }); }); })
      .observe(document.body, { childList: true, subtree: true });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();

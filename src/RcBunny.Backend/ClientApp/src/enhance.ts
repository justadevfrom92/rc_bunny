/*
  ebunny's admin.js table paging, for the React pages: any table in a .table-wrap with more than 10 rows shows
  10 at a time with ebunny's ‹ 1 2 3 › pager under it. Same rules as admin.js: not when the table already has a
  server pager (nav.pager), has inputs in its rows (an edit grid), is a pick list, or says data-page="0".
  It only hides rows and adds the pager after the table, so React's own elements are never moved.
*/

const SIZE = 10;
const state = new WeakMap<HTMLTableElement, { nav: HTMLElement; page: number; rows: number }>();
const navs = new Set<HTMLElement>();

function pageable(table: HTMLTableElement) {
  const wrap = table.parentElement!;
  if (table.dataset.page === '0' || wrap.closest('.pick-list')) return false;
  const next = wrap.nextElementSibling;
  if (next && next.matches('nav.pager') && !navs.has(next as HTMLElement)) return false; // a server pager
  if (wrap.parentElement?.querySelector(':scope > nav.pager:not([data-auto]), :scope > .pager:not([data-auto])')) return false;
  if (table.querySelector('tbody input:not([type=hidden]), tbody select, tbody textarea, [data-filter-row]')) return false;
  return true;
}

function draw(table: HTMLTableElement, nav: HTMLElement, groups: HTMLTableRowElement[][], page: number) {
  const pages = Math.ceil(groups.length / SIZE);
  groups.forEach((g, i) => {
    const on = Math.floor(i / SIZE) === page - 1;
    g.forEach((tr, j) => { tr.hidden = !on || (j > 0 && !tr.classList.contains('open')); });
  });
  let nums = [1, pages];
  for (let n = page - 2; n <= page + 2; n++) if (n > 1 && n < pages) nums.push(n);
  nums = nums.filter((v, i, a) => a.indexOf(v) === i).sort((a, b) => a - b);
  let h = `<button type="button" class="btn sm ghost" data-p="${page - 1}"${page === 1 ? ' disabled aria-disabled="true"' : ''} aria-label="Previous page">‹</button>`;
  nums.forEach((n, i) => {
    if (i && n - nums[i - 1] > 1) h += '<span class="muted">…</span>';
    h += n === page ? `<span class="btn sm on" aria-current="page">${n}</span>` : `<button type="button" class="btn sm ghost" data-p="${n}">${n}</button>`;
  });
  h += `<button type="button" class="btn sm ghost" data-p="${page + 1}"${page === pages ? ' disabled aria-disabled="true"' : ''} aria-label="Next page">›</button>`;
  h += `<span class="muted pager-of">${(page - 1) * SIZE + 1}–${Math.min(page * SIZE, groups.length)} of ${groups.length}</span>`;
  nav.innerHTML = h;
  state.set(table, { nav, page, rows: groups.length });
}

function run() {
  // Pagers whose table is gone
  navs.forEach((nav) => { if (!document.contains(nav) || !nav.previousElementSibling?.querySelector('table')) { nav.remove(); navs.delete(nav); } });
  document.querySelectorAll<HTMLTableElement>('.table-wrap > table').forEach((table) => {
    const body = table.tBodies[0];
    const groups: HTMLTableRowElement[][] = [];
    if (body) Array.from(body.rows).forEach((tr) => { if (tr.hasAttribute('data-attached') && groups.length) groups[groups.length - 1].push(tr); else groups.push([tr]); });
    let s = state.get(table);
    if (!body || groups.length <= SIZE || !pageable(table)) {
      if (s) { groups.flat().forEach((tr) => { tr.hidden = false; }); s.nav.remove(); navs.delete(s.nav); state.delete(table); }
      return;
    }
    if (!s || !document.contains(s.nav)) {
      const nav = document.createElement('nav');
      nav.className = 'pager'; nav.dataset.auto = ''; nav.setAttribute('aria-label', 'Pages');
      nav.addEventListener('click', (e) => {
        const b = (e.target as HTMLElement).closest<HTMLButtonElement>('[data-p]');
        if (b && !b.disabled) { pause(() => draw(table, nav, currentGroups(table), +b.dataset.p!)); }
      });
      table.parentElement!.insertAdjacentElement('afterend', nav);
      navs.add(nav);
      s = { nav, page: 1, rows: -1 };
    }
    // Rows changed (new data): back to page 1; otherwise keep the page you're on
    draw(table, s.nav, groups, s.rows === groups.length ? Math.min(s.page, Math.ceil(groups.length / SIZE)) : 1);
  });
}

function currentGroups(table: HTMLTableElement) {
  const groups: HTMLTableRowElement[][] = [];
  Array.from(table.tBodies[0]?.rows ?? []).forEach((tr) => { if (tr.hasAttribute('data-attached') && groups.length) groups[groups.length - 1].push(tr); else groups.push([tr]); });
  return groups;
}

let observer: MutationObserver | null = null;
let queued = false;

/** Make changes without our own observer reacting to them. */
function pause(fn: () => void) {
  observer?.disconnect();
  try { fn(); } finally { observer?.observe(document.body, { childList: true, subtree: true }); }
}

/** Start watching the page (call once). */
export function enhancePages() {
  observer = new MutationObserver(() => {
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => { queued = false; pause(run); });
  });
  observer.observe(document.body, { childList: true, subtree: true });
  pause(run);
}

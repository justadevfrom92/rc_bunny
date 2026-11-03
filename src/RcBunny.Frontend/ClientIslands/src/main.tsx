/*
  Website script entry (tiny). Razor renders each page completely on the server; an element like
    <div data-island="PlanBrowser">…server HTML…</div><script type="application/json" data-island-props>{…}</script>
  marks a part that becomes interactive. React itself is only downloaded on pages that have an island, and the
  island's data is already in the page, so it renders the moment the code arrives: no fetch, no spinner.
*/
const islands = [...document.querySelectorAll<HTMLElement>('[data-island]')];
if (islands.length) void import('./mount').then((m) => m.mountAll(islands));

// Header menu on phones (the only bit of page chrome that needs script).
document.querySelector<HTMLButtonElement>('[data-menu-toggle]')?.addEventListener('click', (e) => {
  const btn = e.currentTarget as HTMLButtonElement;
  const panel = document.getElementById(btn.getAttribute('aria-controls')!);
  const open = !panel?.classList.contains('open');
  panel?.classList.toggle('open', open);
  btn.setAttribute('aria-expanded', String(open));
});

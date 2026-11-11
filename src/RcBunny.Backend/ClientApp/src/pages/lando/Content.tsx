import { useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, useLoad } from '../../api';
import { slugify } from '../../fields';
import { EmptyRow, PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Lando → Page Templates (ebunny's lando/templates/index). */
export function Templates() {
  const { data, error } = useLoad<any[]>('/api/lando/templates');
  const navigate = useNavigate();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="Page Templates" sub="Layouts that pages are built on." actions={<Link className="btn cyan" to="/lando/templates/create">Add a Template</Link>} />
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Template</th><th>Description</th><th>Layout File</th><th className="num">Pages</th></tr></thead>
        <tbody>{data.map((t) => (
          <tr key={t.id} className="click" onClick={() => navigate(`/lando/templates/${t.id}/edit`)}><td className="mono"><Link to={`/lando/templates/${t.id}/edit`}><b>{t.name}</b></Link></td>
            <td className="wrap">{t.description}</td><td className="mono help">{t.file ?? '—'}</td><td className="num">{t.pages}</td></tr>
        ))}</tbody>
      </table></div></div>
    </>
  );
}

/** Add a Template / Edit Template (ebunny's lando/templates/form). */
export function TemplateForm() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/templates/form${id ? `?id=${id}` : ''}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const t = data.template;
  const save = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), body = { name: f.get('name'), description: f.get('description'), file: f.get('file') };
    run((id ? api.put(`/api/lando/templates/${id}`, body) : api.post('/api/lando/templates', body)).then((r) => { navigate('/lando/templates'); return r; }));
  };
  return (
    <>
      <PageHead title={id ? `Edit Template: ${t.name}` : 'Add a Template'} />
      {bar}
      <form className="panel" onSubmit={save}><div className="panel-body">
        <div className="form-grid">
          <label htmlFor="name">Name</label><input id="name" name="name" required className="mono" defaultValue={t.name} placeholder="e.g. article" />
          <label htmlFor="description">Description</label><input id="description" name="description" required defaultValue={t.description} />
          <label htmlFor="file">Layout File</label><input id="file" name="file" className="mono" defaultValue={t.file ?? ''} placeholder="shared/layouts/article.html (optional)" />
        </div>
        <div className="actions" style={{ marginTop: 20 }}><button className="btn cyan">Save Template</button><Link className="btn ghost" to="/lando/templates">Cancel</Link></div>
      </div></form>
      {id && (
        <div className="panel"><div className="panel-head"><h2>Pages Using This Template</h2><span className="muted">{data.pages.length}</span></div>
          <div className="table-wrap"><table><thead><tr><th>Page</th><th>Path</th><th>Status</th></tr></thead><tbody>
            {data.pages.map((p: any) => <tr key={p.id}><td><Link to={`/lando/pages/${p.id}/edit`}>{p.title}</Link></td><td className="mono">{p.url}</td><td>{p.status}</td></tr>)}
            {data.pages.length === 0 && <EmptyRow cols={3}>No pages use this template yet.</EmptyRow>}
          </tbody></table></div></div>
      )}
    </>
  );
}

/** Lando → Content Blocks (ebunny's lando/blocks/index): the HTML widgets, by category. */
export function Blocks() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const category = params.get('category');
  const { data, error } = useLoad<any>(`/api/lando/blocks${category !== null ? `?category=${category}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const cut = (s: string, n: number) => (s.length > n ? s.slice(0, n - 3) + '...' : s);
  return (
    <>
      <PageHead title="Content Blocks" sub="HTML widgets. Add one to a page as a component (Edit Page → Page Components) or inside page content with its code. Modules take variables, so each page passes its own text."
        actions={<><Link className="btn ghost" to="/lando/block-categories/new">New Category</Link><Link className="btn" to={`/lando/blocks/create${category ? `?category=${category}` : ''}`}>Add a Content Block</Link></>} />
      <div className="actions" style={{ marginBottom: 14 }}>
        <Link className={`btn sm${category === null ? '' : ' ghost'}`} to="/lando/blocks">All Content Blocks</Link>
        {data.categories.map((c: any) => <Link key={c.id} className={`btn sm${category === String(c.id) ? '' : ' ghost'}`} to={`/lando/blocks?category=${c.id}`}>{c.name} <span className="help">{c.blocks}</span></Link>)}
        <Link className={`btn sm${category === '0' ? '' : ' ghost'}`} to="/lando/blocks?category=0">Uncategorized <span className="help">{data.uncategorized}</span></Link>
      </div>
      <div className="panel"><div className="table-wrap"><table className="table blocks-table">
        <thead><tr><th>Block</th><th>Category</th><th>Kind</th><th className="num">On Pages</th><th>Updated</th></tr></thead>
        <tbody>
          {data.blocks.map((b: any) => {
            // Short name for the table: drop the category prefix and " - Primary"; the full name shows on hover
            const short = b.name.replace(/ - Primary$/, '').replace(new RegExp(`^${(b.category ?? '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} - `), '').trim();
            return (
              <tr key={b.id} className="click" onClick={() => navigate(`/lando/blocks/${b.id}/edit`)}>
                <td><Link to={`/lando/blocks/${b.id}/edit`} title={b.name}><b>{cut(short, 34)}</b></Link><br />
                  <span className="help">{b.text !== '' ? cut(b.text, 48) : b.uses ? `Uses module: ${b.uses}` : 'Widgets only'}</span></td>
                <td>{b.category ?? 'Uncategorized'}{b.site && <><br /><span className="help">{b.site} only</span></>}</td>
                <td>{b.variables.length ? <span className="pill info" title={b.variables.join(', ')}>Module · {b.variables.length} {b.variables.length === 1 ? 'variable' : 'variables'}</span> : <span className="help">Fixed text</span>}</td>
                <td className="num">{b.components}</td>
                <td className="nowrap">{new Date(b.updatedAt).toLocaleDateString('en-US', { month: 'numeric', day: 'numeric', year: '2-digit' })}</td></tr>
            );
          })}
          {data.blocks.length === 0 && <EmptyRow cols={5}>No content blocks in this category.</EmptyRow>}
        </tbody>
      </table></div></div>
    </>
  );
}

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
const VAR = /\{\{\s*([a-z][a-z0-9_]*)\s*(?:\|([^}]*))?\}\}/gi;

/** Add a Content Block / Edit (ebunny's lando/blocks/form): the HTML with a live preview and its variables. */
export function BlockForm() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/lando/blocks/form${id ? `?id=${id}` : ''}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return <BlockEditor data={data} id={id} category={params.get('category')} navigate={navigate} key={data.block.id} />;
}

function BlockEditor({ data, id, category, navigate }: { data: any; id?: string; category: string | null; navigate: (to: string) => void }) {
  const b = data.block;
  const [html, setHtml] = useState<string>(b.html ?? '');
  const [run, bar] = useRun();
  const slug = useRef<HTMLInputElement>(null);
  const form = useRef<HTMLFormElement>(null);
  // Variables show their default text (or [name]) in the preview, and are listed under it
  const vars: [string, string][] = [];
  const preview = html.replace(VAR, (_m, name: string, def?: string) => {
    const d = (def ?? '').trim();
    if (!vars.some(([n]) => n === name)) vars.push([name, d]);
    return d ? esc(d) : `[${esc(name)}]`;
  });
  const save = (e?: FormEvent<HTMLFormElement>) => {
    e?.preventDefault();
    const f = new FormData(form.current!), n = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { name: f.get('name'), slug: f.get('slug'), categoryId: n('category_id'), siteId: n('site_id'), html };
    run((id ? api.put<any>(`/api/lando/blocks/${id}`, body) : api.post<any>('/api/lando/blocks', body)).then((r) => { if (!id) navigate(`/lando/blocks/${r.id}/edit`); return r; }));
  };
  const shortcode = `[[block|${b.slug}${Object.entries(data.variables).map(([k, d]) => `|${k}=${(d as string) !== '' ? d : '…'}`).join('')}]]`;
  return (
    <>
      <PageHead title={id ? 'Edit Content Block' : 'Add a Content Block'} sub={id ? <span className="mono">{shortcode}</span> : undefined} />
      {bar}
      <div className="grid-2">
        <form className="panel" ref={form} onSubmit={save}><div className="panel-body">
          <div className="form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label htmlFor="name">Name</label><input id="name" name="name" required defaultValue={b.name} onInput={id ? undefined : (e) => { if (slug.current) slug.current.value = slugify(e.currentTarget.value); }} />
            <label htmlFor="slug">Slug</label><input id="slug" name="slug" required className="mono" defaultValue={b.slug} ref={slug} />
            <label htmlFor="category_id">Category</label>
            <select id="category_id" name="category_id" defaultValue={b.categoryId ?? category ?? ''}><option value="">Uncategorized</option>{data.categories.map((c: any) => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
            <label htmlFor="site_id">Site</label>
            <select id="site_id" name="site_id" defaultValue={b.siteId ?? ''}><option value="">All sites</option>{data.sites.map((s: any) => <option key={s.id} value={s.id}>{s.name} only</option>)}</select>
            <label htmlFor="b-html">Content (HTML)</label>
            <textarea id="b-html" name="html" spellCheck={false} value={html} onChange={(e) => setHtml(e.target.value)}
              onKeyDown={(e) => {
                const t = e.currentTarget;
                if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); save(); }
                if (e.key === 'Tab') { e.preventDefault(); const s = t.selectionStart; setHtml(html.slice(0, s) + '  ' + html.slice(t.selectionEnd)); requestAnimationFrame(() => t.setSelectionRange(s + 2, s + 2)); }
              }} />
          </div>
          <p className="help" style={{ marginTop: 8 }}>Widgets you can use: <span className="mono">[[zip_form]] [[plans|group=featured|limit=3]] [[phone]] [[year]]</span> and other blocks with <span className="mono">[[block|slug]]</span>.</p>
          <p className="help" style={{ marginTop: 6 }}><b>Make it a module:</b> put <span className="mono">{'{{heading}}'}</span> or <span className="mono">{'{{heading|Default text}}'}</span> where a page should pass its own text. Each page fills them in on its component (Edit Page → Page Components) or in the code: <span className="mono">[[block|{b.slug || 'slug'}|heading=Save today]]</span>.</p>
          <div className="actions" style={{ marginTop: 16 }}><button className="btn cyan">Save Content Block</button><Link className="btn ghost" to="/lando/blocks">Cancel</Link></div>
          <p className="help" style={{ marginTop: 10 }}>Ctrl+S saves.</p>
        </div></form>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div className="panel"><div className="panel-head"><h2>Preview</h2><span className="muted">variables show their default text; widget codes shown as-is</span></div>
            <div className="panel-body"><iframe id="b-prev" title="Block preview" sandbox="" style={{ width: '100%', minHeight: 300, border: '1px solid var(--line)', borderRadius: 6, background: '#fff' }}
              srcDoc={`<style>body{font-family:sans-serif;padding:12px;color:#102247}</style>${preview}`} /></div>
          </div>
          {vars.length > 0 && <div className="panel"><div className="panel-head"><h2>Variables</h2><span className="muted">text each page passes in</span></div><div className="table-wrap"><table data-page="0">
            <thead><tr><th>Name</th><th>Default text</th></tr></thead><tbody>{vars.map(([n, d]) => <tr key={n}><td className="mono">{n}</td><td>{d || <span className="muted">none</span>}</td></tr>)}</tbody></table></div></div>}
          {id && data.usedOn.length === 0 && <p className="help">Not placed on any page as a component yet. Add it from a page's Content Blocks, or paste its shortcode into another block.</p>}
          {id && data.usedOn.length > 0 && (
            <div className="panel"><div className="panel-head"><h2>Used On</h2></div><div className="table-wrap"><table>
              <thead><tr><th>Page</th><th>Site</th><th>Zone</th></tr></thead>
              <tbody>{data.usedOn.map((u: any, i: number) => <tr key={i}><td><Link to={`/lando/pages/${u.pageId}/edit#components`}>{u.title}</Link> <span className="mono help">{u.url}</span></td><td>{u.site}</td><td>{u.zone}</td></tr>)}</tbody>
            </table></div></div>
          )}
          {id && data.canDelete && <div><button className="btn red" onClick={() => confirm('Delete this content block?\nIt is also removed from every page it is on.') && run(api.del(`/api/lando/blocks/${id}`).then((r) => { navigate('/lando/blocks'); return r; }))}>Delete Content Block</button></div>}
        </div>
      </div>
    </>
  );
}

/** New Content Block Category (ebunny's lando/blocks/category-form). */
export function CategoryForm() {
  const navigate = useNavigate();
  const [run, bar] = useRun();
  return (
    <>
      <PageHead title="New Content Block Category" sub="Groups content blocks in the list and the page editor." />
      {bar}
      <form className="panel simple-form" onSubmit={(e) => { e.preventDefault(); run(api.post('/api/lando/block-categories', { name: new FormData(e.currentTarget).get('name') }).then((r) => { navigate('/lando/blocks'); return r; })); }}>
        <div className="panel-body form-grid"><label htmlFor="name">Name</label><input id="name" name="name" required maxLength={60} /></div>
        <div className="panel-body actions"><button className="btn cyan">Add Category</button><Link className="btn ghost" to="/lando/blocks">Cancel</Link></div>
      </form>
    </>
  );
}

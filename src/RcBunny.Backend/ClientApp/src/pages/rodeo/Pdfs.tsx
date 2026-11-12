import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { AccountNo, EmptyRow, PageHead, PagerBar, Pill, useRun } from '../../ui';
import { useAuth } from '../../auth';
import { when } from '../walker/shared';

/* eslint-disable @typescript-eslint/no-explicit-any */

const day = (s: string) => new Date(s).toLocaleDateString('en-US');
const kb = (n: number) => `${(n / 1024).toFixed(1)} KB`;

/** The PDF as a file the browser can open or save (ebunny's View PDF / Download PDF from the base64). */
function usePdfUrl(pdf64?: string) {
  const url = useMemo(() => {
    if (!pdf64) return '';
    const bin = atob(pdf64), bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
  }, [pdf64]);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);
  return url;
}

/** ebunny's rodeo/pdfs/_pages: the PDF's pages drawn as images. */
const PdfPages = ({ pages }: { pages: string[] }) => (
  <div className="pdf-viewer pdf-pages">
    {pages.map((svg, i) => <div key={i} className="pdf-page" aria-label={`Page ${i + 1} of ${pages.length}`} dangerouslySetInnerHTML={{ __html: svg }} />)}
  </div>
);

/** Rodeo → PDF Templates (ebunny's rodeo/pdfs/index). */
export function PdfTemplates() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/pdfs?${sp}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="PDF Templates" sub={<>Letters and notices made into PDFs for an account, written like email templates with fill-ins such as <span className="mono">{'{{first_name}}'}</span> and <span className="mono">{'{{balance}}'}</span>.</>}
        actions={<Link className="btn cyan" to="/rodeo/pdfs/new">New PDF Template</Link>} />
      <select className="pill-select" aria-label="Category" style={{ marginBottom: 18 }} value={data.only ?? ''} onChange={(e) => navigate(`/rodeo/pdfs${e.target.value ? `?category=${encodeURIComponent(e.target.value)}` : ''}`)}>
        <option value="">All categories · {data.all}</option>
        {data.categories.map((c: string) => <option key={c} value={c}>{c} · {data.counts[c] ?? 0}</option>)}
      </select>
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>ID</th><th>Template</th><th>Category</th><th>Status</th><th className="num">PDFs Made</th><th>Last Made</th><th>Updated</th></tr></thead>
        <tbody>
          {data.templates.map((t: any) => (
            <tr key={t.id} className="click" onClick={() => navigate(`/rodeo/pdfs/${t.id}`)}><td className="nowrap"><Link to={`/rodeo/pdfs/${t.id}`}><b>#{t.id}</b></Link></td>
              <td><b>{t.name}</b><div className="help">{t.title}</div></td><td>{t.category ?? '—'}</td><td><Pill tone={t.status === 'Active' ? 'ok' : 'warn'}>{t.status}</Pill></td>
              <td className="num">{t.made.toLocaleString()}</td><td className="nowrap">{t.lastMade ? day(t.lastMade) : '—'}</td><td className="nowrap">{day(t.updatedAt)}</td></tr>
          ))}
          {data.templates.length === 0 && <EmptyRow cols={7}>No PDF templates{data.only ? ` in ${data.only}` : ''}.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={10} onPage={(p) => navigate(`/rodeo/pdfs?${qs({ category: data.only, page: p })}`)} /></div>
    </>
  );
}

/** New PDF Template and a template's page (ebunny's rodeo/pdfs/form). */
export function PdfTemplateForm() {
  const { id } = useParams();
  const [sp, setSp] = useSearchParams();
  const navigate = useNavigate();
  const { me } = useAuth();
  const account = sp.get('account'), find = sp.get('find');
  const { data, error, reload } = useLoad<any>(`/api/rodeo/pdfs/${id ?? 'new'}?${qs({ account, find })}`);
  const [run, bar] = useRun(reload);
  const url = usePdfUrl(data?.preview?.pdf64);
  const [preview, setPreview] = useState(account ?? '');
  const [search, setSearch] = useState(find ?? '');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const t = data.template;
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const n = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
    const body = { name: f.get('name'), category: f.get('category'), status: f.get('status'), title: f.get('title'), body: f.get('body'), footer: f.get('footer'),
      pdfHeaderId: n('pdf_header_id'), pdfFooterId: n('pdf_footer_id'), showAddress: f.get('show_address') === 'on' };
    run((id ? api.put<any>(`/api/rodeo/pdfs/${id}`, body) : api.post<any>('/api/rodeo/pdfs', body)).then((r) => { if (!id) navigate(`/rodeo/pdfs/${r.id}`); return r; }));
  };
  const keep = (o: Record<string, string | null>) => setSp(Object.fromEntries(Object.entries({ account, find, ...o }).filter(([, v]) => v)) as Record<string, string>);
  const make = (acct: string) => run(api.post<any>(`/api/rodeo/pdfs/${id}/make`, { account: acct }).then((r) => { navigate(`/rodeo/pdfs/made/${r.id}`); return r; }));
  const test = (e: React.FormEvent<HTMLFormElement>) => { e.preventDefault(); const f = new FormData(e.currentTarget); run(api.post(`/api/rodeo/pdfs/${id}/test`, { to: f.get('to'), account: f.get('account') })); };
  const updated = new Date(t.updatedAt).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
  return (
    <>
      <PageHead title={id ? t.name : 'New PDF Template'} sub={id ? `Template #${id} · ${t.category ?? 'No category'} · updated ${updated}` : 'Write the letter, save it, then preview and make PDFs for accounts.'}
        actions={<><Link className="btn ghost btn-back" to="/rodeo/pdfs">Back to PDF Templates</Link><a className="btn cyan" href={url} target="_blank" rel="noopener">View PDF</a><a className="btn ghost" href={url} download={data.fileName}>Download PDF</a></>} />
      {bar}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <form className="panel" onSubmit={save} key={t.updatedAt}>
          <div className="panel-head"><h2>Template</h2></div>
          <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={100} defaultValue={t.name} />
            <label htmlFor="category">Category</label><select id="category" name="category" defaultValue={t.category ?? ''}><option value="">None</option>{data.categories.map((c: string) => <option key={c}>{c}</option>)}</select>
            <label htmlFor="status">Status</label><select id="status" name="status" defaultValue={t.status}>{['Active', 'Draft'].map((s) => <option key={s}>{s}</option>)}</select>
            <label htmlFor="title">Title on the PDF</label><input id="title" name="title" required maxLength={120} defaultValue={t.title} />
            <label htmlFor="body">Body</label>
            <div><textarea id="body" name="body" required spellCheck style={{ minHeight: 320, fontFamily: 'var(--font-mono, monospace)', fontSize: '.88rem' }} defaultValue={t.body} />
              <div className="help">One block per line: <span className="mono"># Heading</span>, <span className="mono">## Smaller heading</span>, <span className="mono">- bullet</span>, <span className="mono">| Label | Value |</span> for a shaded row, <span className="mono">**bold line**</span>, <span className="mono">---</span> for a line. A blank line starts a new paragraph.</div></div>
            <label htmlFor="pdf_header_id">Header</label><select id="pdf_header_id" name="pdf_header_id" defaultValue={t.pdfHeaderId ?? ''}><option value="">Default header</option>{data.headers.map((h: any) => <option key={h.id} value={h.id}>{h.name}{h.isDefault ? ' (default)' : ''}</option>)}</select>
            <label htmlFor="pdf_footer_id">Footer</label><div><select id="pdf_footer_id" name="pdf_footer_id" defaultValue={t.pdfFooterId ?? ''}><option value="">Default footer</option>{data.footers.map((h: any) => <option key={h.id} value={h.id}>{h.name}{h.isDefault ? ' (default)' : ''}</option>)}</select>
              <div className="help">Headers and footers are under <Link to="/rodeo/pdf-headers-footers">PDF Headers &amp; Footers</Link>.</div></div>
            <label htmlFor="footer">This template's own footer (optional: used instead of the footer above)</label><textarea id="footer" name="footer" maxLength={600} style={{ minHeight: 70 }} defaultValue={t.footer ?? ''} />
            <label htmlFor="show_address">Account block</label><label className="check"><input id="show_address" type="checkbox" name="show_address" defaultChecked={t.showAddress} /> Show the customer's name, address and account number on the first page</label>
          </div>
          <div className="panel-body"><button className="btn cyan">Save Template</button>{id && <span className="help" style={{ marginLeft: 10 }}>Save to update the preview.</span>}</div>
        </form>
        {id && <div style={{ marginTop: -8 }}><button className="btn sm ghost" onClick={() => confirm(`Delete ${t.name}?\nThe template and the ${data.made} PDFs made from it are deleted.`) && run(api.del(`/api/rodeo/pdfs/${id}`).then((r) => { navigate('/rodeo/pdfs'); return r; }))}>Delete Template</button></div>}
        <div className="panel"><div className="panel-head"><h2>Fill-Ins</h2><span className="muted">type these anywhere in the title, body or footer</span></div><div className="table-wrap"><table data-page="0">
          <thead><tr><th>Fill-In</th><th>What It Becomes</th><th>For {data.sample?.firstName ?? 'the sample'}</th></tr></thead>
          <tbody>{data.fields.map((f: any) => <tr key={f.key}><td className="mono">{`{{${f.key}}}`}</td><td>{f.label}</td><td>{f.value}</td></tr>)}</tbody>
        </table></div></div>
        <div className="panel" id="preview"><div className="panel-head"><h2>Preview</h2><span className="muted">{data.preview.pages.length} {data.preview.pages.length === 1 ? 'page' : 'pages'} · for {data.sample?.name ?? 'a sample account'} ({data.sample?.account})</span></div>
          <form className="panel-body form-row" onSubmit={(e) => { e.preventDefault(); keep({ account: preview || null }); }}><input aria-label="Preview for account number" placeholder="Preview for another account number" value={preview} onChange={(e) => setPreview(e.target.value)} inputMode="numeric" /><button className="btn sm">Preview</button></form>
          <PdfPages pages={data.preview.pages} />
        </div>
        {id && (
          <>
            {/* Make a PDF for an Account: find the customer, then make it (kept under PDFs Made, with a note on the account) */}
            <div className="panel customer-finder" id="make-pdf">
              <div className="panel-head"><h2>Make a PDF for an Account</h2></div>
              <div className="panel-body">
                <form className="adv-bar" role="search" onSubmit={(e) => { e.preventDefault(); keep({ find: search || null }); }}>
                  <label className="search-field"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>
                    <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name, last name, phone, account number, email or address" aria-label="Find a customer" /></label>
                  <button className="btn cyan">Search</button>
                </form>
                <p className="help" style={{ marginTop: 8 }}>Find the customer, then Make PDF. The PDF is kept under PDFs Made, and a note is added to the account.</p>
              </div>
              {data.found && (
                <div className="table-wrap"><table data-page="0">
                  <thead><tr><th>ID</th><th>Account</th><th>Name</th><th>Phone</th><th>Email</th><th>Status</th><th><span className="sr-only">Action</span></th></tr></thead>
                  <tbody>
                    {data.found.map((c: any) => (
                      <tr key={c.id}><td className="nowrap"><Link to={`/corral/customers/${c.id}`}><b>#{c.id}</b></Link></td><td><AccountNo id={c.id} account={c.account} /></td>
                        <td><b>{c.name}</b><div className="help">{c.city}</div></td><td className="nowrap">{c.phone}</td><td className="wrap">{c.email}</td><td>{c.status}</td>
                        <td className="nowrap" style={{ textAlign: 'right' }}><button className="btn sm cyan" onClick={() => make(c.account)}>Make PDF</button></td></tr>
                    ))}
                    {data.found.length === 0 && <EmptyRow cols={7}>No customers match “{find}”.</EmptyRow>}
                  </tbody>
                </table></div>
              )}
            </div>
            {/* Send a Test: the PDF attached to an email marked as a test */}
            <form className="panel" id="send-test" onSubmit={test}>
              <div className="panel-head"><h2>Send a Test PDF by Email</h2></div>
              <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
                <label htmlFor="test-to">Send to</label><input id="test-to" name="to" required maxLength={600} defaultValue={me?.email} placeholder="you@example.com, teammate@example.com" />
                <span className="help">Up to 5 addresses, separated by commas. The email says it's a test, with the PDF attached.</span>
                <label htmlFor="test-account">Fill in for account</label><input id="test-account" name="account" inputMode="numeric" maxLength={20} defaultValue={account ?? data.sample?.account} placeholder="Account number (blank: the sample account)" />
              </div>
              <div className="panel-body"><button className="btn cyan">Send Test</button>{!data.delivers && <span className="help" style={{ marginLeft: 10 }}>Mail is written to the mail log here (no SMTP host set); set Integrations:Smtp:Host to deliver to inboxes.</span>}</div>
              {data.tests.length > 0 && (
                <div className="table-wrap"><table data-page="0">
                  <thead><tr><th>ID</th><th>Sent</th><th>To</th><th>Filled In For</th><th>By</th><th>Result</th></tr></thead>
                  <tbody>{data.tests.map((x: any) => (
                    <tr key={x.id} className="click" onClick={() => navigate(`/rodeo/records/pdf_test/${x.id}`)}><td className="nowrap"><Link to={`/rodeo/records/pdf_test/${x.id}`}><b>#{x.id}</b></Link></td><td className="nowrap">{when(x.createdAt)}</td>
                      <td className="wrap">{x.recipients}</td><td>{x.account ?? '—'}</td><td>{x.by ?? '—'}</td>
                      <td><Pill tone={x.status === 'sent' ? 'ok' : 'bad'}>{x.status.charAt(0).toUpperCase() + x.status.slice(1)}</Pill>{x.error && <div className="help">{x.error.length > 80 ? x.error.slice(0, 77) + '...' : x.error}</div>}</td></tr>
                  ))}</tbody>
                </table></div>
              )}
            </form>
            <div className="panel"><div className="panel-head"><h2>PDFs Made From This Template</h2><Link className="btn sm ghost" to={`/rodeo/pdfs/made?template=${id}`}>All</Link></div><div className="table-wrap"><table data-page="0">
              <thead><tr><th>ID</th><th>Made</th><th>Account</th><th>By</th><th className="num">Pages</th></tr></thead>
              <tbody>
                {data.recent.map((g: any) => (
                  <tr key={g.id} className="click" onClick={() => navigate(`/rodeo/pdfs/made/${g.id}`)}><td className="nowrap"><Link to={`/rodeo/pdfs/made/${g.id}`}><b>#{g.id}</b></Link></td><td className="nowrap">{when(g.createdAt)}</td>
                    <td onClick={(e) => e.stopPropagation()}>{g.customer ? <><AccountNo id={g.customer.id} account={g.customer.account} /><div className="help">{g.customer.name}</div></> : '—'}</td>
                    <td>{g.by ?? 'System'}</td><td className="num">{g.pages}</td></tr>
                ))}
                {data.recent.length === 0 && <EmptyRow cols={5}>None made yet.</EmptyRow>}
              </tbody>
            </table></div></div>
          </>
        )}
      </div>
    </>
  );
}

/** Rodeo → PDFs Made (ebunny's rodeo/pdfs/generated). */
export function PdfsMade() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/rodeo/pdfs/made?${sp}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="PDFs Made" sub="Every PDF made from a template for an account, with the wording it was made from." />
      <select className="pill-select" aria-label="Template" style={{ marginBottom: 18 }} value={data.only ?? ''} onChange={(e) => navigate(`/rodeo/pdfs/made${e.target.value ? `?template=${e.target.value}` : ''}`)}>
        <option value="">All templates</option>{data.templates.map((t: any) => <option key={t.id} value={t.id}>{t.name}</option>)}
      </select>
      <div className="panel"><div className="table-wrap"><table data-page="0">
        <thead><tr><th>ID</th><th>Made</th><th>Template</th><th>Account</th><th>File</th><th>By</th><th className="num">Pages</th></tr></thead>
        <tbody>
          {data.rows.map((g: any) => (
            <tr key={g.id} className="click" onClick={() => navigate(`/rodeo/pdfs/made/${g.id}`)}><td className="nowrap"><Link to={`/rodeo/pdfs/made/${g.id}`}><b>#{g.id}</b></Link></td><td className="nowrap">{when(g.createdAt)}</td>
              <td><b>{g.template?.name}</b><div className="help">{g.template?.category}</div></td>
              <td onClick={(e) => e.stopPropagation()}>{g.customer ? <><AccountNo id={g.customer.id} account={g.customer.account} /><div className="help">{g.customer.name}</div></> : '—'}</td>
              <td className="mono wrap">{g.fileName}<div className="help">{kb(g.size)}</div></td><td>{g.by ?? 'System'}</td><td className="num">{g.pages}</td></tr>
          ))}
          {data.rows.length === 0 && <EmptyRow cols={7}>No PDFs made yet.</EmptyRow>}
        </tbody>
      </table></div>
        <PagerBar page={data.page} total={data.total} size={10} onPage={(p) => navigate(`/rodeo/pdfs/made?${qs({ template: data.only, page: p })}`)} /></div>
    </>
  );
}

/** One PDF made (ebunny's rodeo/pdfs/generated-show). */
export function PdfMade() {
  const { id } = useParams();
  const { data, error } = useLoad<any>(`/api/rodeo/pdfs/made/${id}`);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const g = data.made;
  return (
    <>
      <PageHead title={g.title} sub={`PDF #${g.id} · made ${when(g.createdAt)} by ${g.by ?? 'System'}`}
        actions={<><Link className="btn ghost btn-back" to="/rodeo/pdfs/made">Back to PDFs Made</Link><a className="btn cyan" href={`/api/rodeo/pdfs/made/${g.id}/pdf`} target="_blank" rel="noopener">View PDF</a>
          <a className="btn ghost" href={`/api/rodeo/pdfs/made/${g.id}/pdf?download=true`}>Download PDF</a></>} />
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div className="panel"><div className="panel-head"><h2>Details</h2></div><div className="panel-body"><dl className="kv">
          <dt>PDF</dt><dd className="mono">#{g.id} · {g.fileName}</dd>
          <dt>Template</dt><dd>{g.template ? <Link to={`/rodeo/pdfs/${g.template.id}`}>#{g.template.id} {g.template.name}</Link> : '—'}</dd>
          <dt>Account</dt><dd>{g.customer ? <><AccountNo id={g.customer.id} account={g.customer.account} /> {g.customer.name}</> : '—'}</dd>
          <dt>Pages</dt><dd>{g.pages} · {kb(g.size)}</dd>
          <dt>Made By</dt><dd>{g.by ?? 'System'}</dd>
        </dl></div></div>
        <div className="panel"><div className="panel-head"><h2>The PDF</h2><span className="muted">{data.preview.pages.length} {data.preview.pages.length === 1 ? 'page' : 'pages'}</span></div>
          <PdfPages pages={data.preview.pages} /></div>
      </div>
    </>
  );
}

/** Rodeo → PDF Headers & Footers (ebunny's rodeo/pdfs/parts). */
export function PdfParts() {
  const navigate = useNavigate();
  const { data, error } = useLoad<any>('/api/rodeo/pdf-headers-footers');
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  return (
    <>
      <PageHead title="PDF Headers & Footers" sub="The band across the top of a PDF and the small print at the bottom of every page. Each PDF template picks a header and a footer; templates that don't pick one use the default."
        actions={<><Link className="btn ghost" to="/rodeo/pdf-headers-footers/new?kind=header">New Header</Link><Link className="btn cyan" to="/rodeo/pdf-headers-footers/new?kind=footer">New Footer</Link></>} />
      {(['header', 'footer'] as const).map((kind) => (
        <div key={kind} className="panel"><div className="panel-head"><h2>{kind === 'header' ? 'Headers' : 'Footers'}</h2></div><div className="table-wrap"><table>
          <thead><tr><th>ID</th><th>Name</th><th>{kind === 'header' ? 'Looks Like' : 'Small Print'}</th><th>Default</th><th className="num">Templates Using It</th><th>Changed</th></tr></thead>
          <tbody>
            {data[kind].map((p: any) => (
              <tr key={p.id} className="click" onClick={() => navigate(`/rodeo/pdf-headers-footers/${p.id}`)}><td className="nowrap"><Link to={`/rodeo/pdf-headers-footers/${p.id}`}><b>#{p.id}</b></Link></td><td><b>{p.name}</b></td>
                <td className="wrap help">{p.looks.length > 90 ? p.looks.slice(0, 87) + '...' : p.looks}</td><td>{p.isDefault && <Pill tone="ok">Default</Pill>}</td>
                <td className="num">{p.templates}</td><td className="nowrap">{day(p.updatedAt)}</td></tr>
            ))}
            {data[kind].length === 0 && <EmptyRow cols={6}>No {kind}s yet.</EmptyRow>}
          </tbody>
        </table></div></div>
      ))}
    </>
  );
}

/** New / edit a PDF header or footer (ebunny's rodeo/pdfs/part-form). */
export function PdfPartForm() {
  const { id } = useParams();
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const { data, error, reload } = useLoad<any>(`/api/rodeo/pdf-headers-footers/${id ?? 'new'}?${qs({ kind: sp.get('kind') })}`);
  const [run, bar] = useRun(reload);
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.part, label = p.kind === 'header' ? 'Header' : 'Footer';
  const save = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.currentTarget)) as Record<string, string>;
    const body = { ...f, kind: p.kind, isDefault: f.is_default === 'on' };
    run((id ? api.put<any>(`/api/rodeo/pdf-headers-footers/${id}`, body) : api.post<any>('/api/rodeo/pdf-headers-footers', body)).then((r) => { if (!id) navigate(`/rodeo/pdf-headers-footers/${r.id}`); return r; }));
  };
  const updated = p.updatedAt && new Date(p.updatedAt).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
  return (
    <>
      <PageHead title={id ? p.name : `New PDF ${label}`} sub={id ? `PDF ${p.kind} #${id}${p.isDefault ? ' · the default' : ''} · updated ${updated}` : 'Save it, then pick it on a PDF template (or make it the default).'}
        actions={<Link className="btn ghost btn-back" to="/rodeo/pdf-headers-footers">Back to PDF Headers &amp; Footers</Link>} />
      {bar}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <form className="panel" onSubmit={save} key={updated ?? 'new'}>
          <div className="panel-head"><h2>{label}</h2></div>
          <div className="panel-body form-grid" style={{ gridTemplateColumns: '1fr' }}>
            <label htmlFor="name">Name</label><input id="name" name="name" required maxLength={100} defaultValue={p.name} />
            {p.kind === 'header' ? <>
              <label htmlFor="title">Big text in the band</label><input id="title" name="title" maxLength={120} placeholder="Energy Texas" defaultValue={p.title ?? ''} />
              <label htmlFor="subtitle">Line under it</label><input id="subtitle" name="subtitle" maxLength={160} placeholder="Blank: the PDF's title" defaultValue={p.subtitle ?? ''} />
              <label htmlFor="color">Look</label><select id="color" name="color" defaultValue={p.color}>{Object.entries(data.colors as Record<string, string>).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select>
            </> : <>
              <label htmlFor="body">Small print on every page</label><textarea id="body" name="body" maxLength={600} style={{ minHeight: 90 }} defaultValue={p.body ?? ''} />
              <span className="help">A PDF template's own footer text, when it has one, is used instead.</span>
            </>}
            <label htmlFor="is_default">Default</label><label className="check"><input id="is_default" type="checkbox" name="is_default" defaultChecked={p.isDefault} /> Use this {p.kind} on templates that don't pick one</label>
            <span className="help">Fill-ins like <span className="mono">{'{{brand}}'}</span>, <span className="mono">{'{{phone}}'}</span> or <span className="mono">{'{{account}}'}</span> work here too.</span>
          </div>
          <div className="panel-body"><button className="btn cyan">Save {label}</button></div>
        </form>
        {id && !p.isDefault && <div style={{ marginTop: -8 }}><button className="btn sm ghost" onClick={() => confirm(`Delete ${p.name}?\nTemplates using it go back to the default ${p.kind}.`) && run(api.del(`/api/rodeo/pdf-headers-footers/${id}`).then((r) => { navigate('/rodeo/pdf-headers-footers'); return r; }))}>Delete {label}</button></div>}
        <div className="panel"><div className="panel-head"><h2>Preview</h2><span className="muted">on a sample letter{id ? '' : ' (save to see your changes)'}</span></div><PdfPages pages={data.preview.pages} /></div>
        {id && (
          <div className="panel"><div className="panel-head"><h2>PDF Templates Using It</h2></div><div className="table-wrap"><table data-page="0">
            <thead><tr><th>ID</th><th>Template</th><th>Category</th><th>Status</th></tr></thead>
            <tbody>
              {data.templates.map((t: any) => <tr key={t.id} className="click" onClick={() => navigate(`/rodeo/pdfs/${t.id}`)}><td className="nowrap"><Link to={`/rodeo/pdfs/${t.id}`}><b>#{t.id}</b></Link></td><td>{t.name}</td><td>{t.category ?? '—'}</td><td>{t.status}</td></tr>)}
              {data.templates.length === 0 && <EmptyRow cols={4}>No templates use it yet.</EmptyRow>}
            </tbody>
          </table></div></div>
        )}
      </div>
    </>
  );
}

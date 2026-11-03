import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, post, put, queryClient } from '../api';
import { handleForbidden } from '../auth';
import { ErrorBox, num, Panel, Refreshing, Skeleton } from '../ui';
import { rowQuery, useList, useMeta, type ActionMeta, type Field, type ResourceMeta } from './api';
import { FullValue, Input, Value } from './fields';

/*
  The generic admin screens, driven by /api/r/{key}/meta. Routes: /:app/r/:key, /:app/r/:key/new, /:app/r/:key/:id, /:app/r/:key/:id/edit.
  Lists keep their search, filters, sort and page in the URL; rows prefetch the record on hover; writes refresh the app's cache.
*/

const base = (app: string, key: string) => `/${app}/r/${key}`;
const invalidateApp = (app: string) => queryClient.invalidateQueries({ queryKey: [app] });

export function ResourceList() {
  const { app = '', key = '' } = useParams();
  const meta = useMeta(key);
  if (meta.error) return <ErrorBox error={meta.error} />;
  if (!meta.data) return <div className="panel"><Skeleton rows={8} cols={5} /></div>;
  return (
    <>
      <div className="page-head">
        <div>
          <h1>{meta.data.title}</h1>
          {meta.data.description && <p className="muted">{meta.data.description}</p>}
        </div>
        {meta.data.create && <Link className="btn" to={`${base(app, key)}/new`}>+ New {meta.data.name}</Link>}
      </div>
      <ResourceTable app={app} meta={meta.data} />
    </>
  );
}

/** The table; also used for related lists on a record page (fixed = filters that can't change). */
export function ResourceTable({ app, meta, fixed, compact }: { app: string; meta: ResourceMeta; fixed?: Record<string, string>; compact?: boolean }) {
  const [params, setParams] = useSearchParams();
  const [local, setLocal] = useState<Record<string, string>>({});
  // Related lists keep their own state instead of the page URL
  const get = (k: string) => (fixed ? local[k] : params.get(k)) ?? undefined;
  const set = (changes: Record<string, string | undefined>) => {
    if (fixed) { setLocal((l) => ({ ...l, ...Object.fromEntries(Object.entries(changes).map(([k, v]) => [k, v ?? ''])) })); return; }
    const p = new URLSearchParams(params);
    for (const [k, v] of Object.entries(changes)) (v ? p.set(k, v) : p.delete(k));
    setParams(p, { replace: true });
  };
  const [q, setQ] = useState(get('q') ?? '');
  useEffect(() => {
    const t = setTimeout(() => { if ((get('q') ?? '') !== q) set({ q: q || undefined, page: undefined }); }, 300);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q]);

  const query: Record<string, string | number | undefined> = { q: get('q'), page: get('page') ?? 1, limit: get('limit') ?? (compact ? 10 : 25), sort: get('sort') };
  for (const f of meta.filters) query[`f.${f}`] = get(`f.${f}`);
  if (!fixed) for (const [k, v] of params) if (k.startsWith('f.')) query[k] = v;
  for (const [k, v] of Object.entries(fixed ?? {})) query[`f.${k}`] = v;

  const list = useList(app, meta.key, query);
  const fields = Object.fromEntries(meta.fields.map((f) => [f.name, f]));
  const cols = meta.list.map((c) => fields[c]).filter((f) => f && !(fixed && f.name in fixed));
  const sort = String(query.sort ?? meta.sort);
  const pages = list.data ? Math.max(1, Math.ceil(list.data.total / list.data.limit)) : 1;
  const page = Number(query.page);

  return (
    <Panel actions={<Refreshing on={list.isFetching && !list.isPending} />}>
      {(meta.search || meta.filters.length > 0) && (
        <div className="filters">
          {meta.search && <input type="search" placeholder="Search…" value={q} onChange={(e) => setQ(e.target.value)} aria-label={`Search ${meta.title}`} />}
          {meta.filters.filter((f) => !(fixed && f in fixed)).map((name) => <FilterInput key={name} field={fields[name]} value={get(`f.${name}`) ?? ''} onChange={(v) => set({ [`f.${name}`]: v || undefined, page: undefined })} />)}
        </div>
      )}
      <ErrorBox error={list.error} />
      {list.isPending ? <Skeleton rows={compact ? 4 : 10} cols={cols.length} /> : list.data!.rows.length === 0 ? <p className="muted">Nothing here yet.</p> : (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                {cols.map((f) => (
                  <th key={f.name}>
                    <button className="th-sort" onClick={() => set({ sort: sort === f.prop ? `-${f.prop}` : f.prop })}>
                      {f.label}{sort === f.prop ? ' ▲' : sort === `-${f.prop}` ? ' ▼' : ''}
                    </button>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {list.data!.rows.map((r) => (
                <tr key={r._id} className="clickable" onMouseEnter={() => void queryClient.prefetchQuery(rowQuery(app, meta.key, r._id))}>
                  {cols.map((f, i) => (
                    <td key={f.name}>
                      {i === 0 ? <Link to={`${base(app, meta.key)}/${encodeURIComponent(r._id)}`}><Value field={f} value={r[f.name]} labels={list.data!.labels[f.name]} /></Link>
                        : <Value field={f} value={r[f.name]} labels={list.data!.labels[f.name]} />}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {list.data && (
        <div className="pager">
          <button className="btn outline small" disabled={page <= 1} onClick={() => set({ page: String(page - 1) })}>← Prev</button>
          <span className="muted small">{num(list.data.total)} · page {page} of {pages}</span>
          <button className="btn outline small" disabled={page >= pages} onClick={() => set({ page: String(page + 1) })}>Next →</button>
        </div>
      )}
    </Panel>
  );
}

function FilterInput({ field, value, onChange }: { field: Field; value: string; onChange: (v: string) => void }) {
  if (!field) return null;
  if (field.type === 'bool')
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={field.label}>
        <option value="">{field.label}: any</option><option value="true">{field.label}: yes</option><option value="false">{field.label}: no</option>
      </select>
    );
  if (field.options)
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={field.label}>
        <option value="">All {field.label.toLowerCase()}</option>
        {field.options.map((o) => <option key={o}>{o}</option>)}
      </select>
    );
  if (field.type === 'ref') return <RefFilter field={field} value={value} onChange={onChange} />;
  return <input placeholder={field.label} value={value} onChange={(e) => onChange(e.target.value)} aria-label={field.label} />;
}

function RefFilter({ field, value, onChange }: { field: Field; value: string; onChange: (v: string) => void }) {
  const opts = useQuery({ queryKey: ['options', field.ref, ''], queryFn: () => api<{ key: string; value: string }[]>(`/api/r/options/${field.ref}`), staleTime: 5 * 60_000 });
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={field.label}>
      <option value="">All {field.label.toLowerCase()}</option>
      {opts.data?.map((o) => <option key={o.key} value={o.key}>{o.value}</option>)}
    </select>
  );
}

export function ResourceDetail() {
  const { app = '', key = '', id = '' } = useParams();
  const meta = useMeta(key);
  const row = useQuery(rowQuery(app, key, id));
  const navigate = useNavigate();
  const del = useMutation({
    mutationFn: () => api(`/api/r/${key}/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    onSuccess: async () => { await invalidateApp(app); navigate(base(app, key)); },
    onError: handleForbidden,
  });
  if (meta.error || row.error) return <ErrorBox error={meta.error ?? row.error} />;
  if (!meta.data || !row.data) return <div className="panel"><Skeleton rows={10} cols={2} /></div>;
  const m = meta.data;
  const r = row.data.row;
  const actions = m.actions.filter((a) => row.data.actions.includes(a.key));

  return (
    <>
      <p className="crumbs"><Link to={base(app, key)}>{m.title}</Link> › {r._label}</p>
      <div className="page-head">
        <h1>{r._label}</h1>
        <div className="row">
          {actions.map((a) => <ActionButton key={a.key} app={app} resource={key} id={id} action={a} />)}
          {m.edit && <Link className="btn" to={`${base(app, key)}/${encodeURIComponent(id)}/edit`}>Edit</Link>}
          {m.delete && <button className="btn outline danger" disabled={del.isPending} onClick={() => confirm(`Delete this ${m.name.toLowerCase()}? This can't be undone.`) && del.mutate()}>Delete</button>}
        </div>
      </div>
      <ErrorBox error={del.error} />
      <Panel>
        <dl className="facts wide-facts">
          {m.fields.map((f) => (
            <div key={f.name} className={f.type === 'text' || f.type === 'json' ? 'span-all' : ''}>
              <dt>{f.label}</dt>
              <dd><FullValue field={f} value={r[f.name]} labels={r._labels[f.name]} /></dd>
            </div>
          ))}
        </dl>
      </Panel>
      {m.children.map((c) => <ChildList key={c.resource + c.foreignKey} app={app} title={c.title} resource={c.resource} foreignKey={c.foreignKey} id={id} />)}
    </>
  );
}

function ChildList({ app, title, resource, foreignKey, id }: { app: string; title: string; resource: string; foreignKey: string; id: string }) {
  const meta = useMeta(resource);
  if (!meta.data) return null;
  const childApp = meta.data.app;
  return (
    <section className="child">
      <div className="page-head">
        <h2>{title}</h2>
        {meta.data.create && <Link className="btn outline small" to={`${base(childApp, resource)}/new?${foreignKey}=${encodeURIComponent(id)}`}>+ Add</Link>}
      </div>
      <ResourceTable app={childApp || app} meta={meta.data} fixed={{ [foreignKey]: id }} compact />
    </section>
  );
}

function ActionButton({ app, resource, id, action }: { app: string; resource: string; id: string; action: ActionMeta }) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [values, setValues] = useState<Record<string, string>>({});
  const [done, setDone] = useState<string | null>(null);
  const run = useMutation({
    mutationFn: () => post<{ message: string }>(`/api/r/${resource}/${encodeURIComponent(id)}/actions/${action.key}`,
      Object.fromEntries(Object.entries(values).map(([k, v]) => [k.replace(/^note:/, ''), v]))),
    onSuccess: async (res) => { setDone(res.message); setOpen(false); setValues({}); await qc.invalidateQueries({ queryKey: [app] }); },
    onError: handleForbidden,
  });
  const inputs = Object.entries(action.inputs);
  const start = () => {
    setDone(null);
    if (inputs.length) setOpen(true);
    else if (!action.confirm || confirm(action.confirm)) run.mutate();
  };
  return (
    <span className="action">
      <button className={`btn outline ${action.tone}`} disabled={run.isPending} onClick={start}>{run.isPending ? '…' : action.label}</button>
      {done && <span className="toast" role="status" onClick={() => setDone(null)}>{done}</span>}
      {!!run.error && !open && <span className="toast bad" role="alert" onClick={() => run.reset()}>{(run.error as Error).message}</span>}
      {open && (
        <div className="modal-back" onClick={() => setOpen(false)}>
          <form className="panel modal" onClick={(e) => e.stopPropagation()} onSubmit={(e) => { e.preventDefault(); run.mutate(); }}>
            <h2>{action.label}</h2>
            {action.confirm && <p>{action.confirm}</p>}
            {inputs.map(([name, label]) => (
              <label key={name}>
                {label}
                {name.startsWith('note:')
                  ? <textarea rows={4} value={values[name] ?? ''} onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))} />
                  : <input value={values[name] ?? ''} onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))} />}
              </label>
            ))}
            <ErrorBox error={run.error} />
            <div className="row">
              <button className="btn" disabled={run.isPending}>{action.label}</button>
              <button type="button" className="btn outline" onClick={() => setOpen(false)}>Cancel</button>
            </div>
          </form>
        </div>
      )}
    </span>
  );
}

export function ResourceForm() {
  const { app = '', key = '', id } = useParams();
  const [params] = useSearchParams();
  const meta = useMeta(key);
  const row = useQuery({ ...rowQuery(app, key, id ?? ''), enabled: !!id });
  const navigate = useNavigate();
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [ready, setReady] = useState(!id);

  useEffect(() => {
    if (id && row.data) { setValues(row.data.row); setReady(true); }
  }, [id, row.data]);
  useEffect(() => {
    // New record from a related list: ?customerId=12 pre-fills the parent
    if (!id && meta.data) {
      const pre: Record<string, unknown> = {};
      for (const f of meta.data.fields) if (params.get(f.name)) pre[f.name] = params.get(f.name);
      setValues(pre);
    }
  }, [id, meta.data, params]);

  const save = useMutation({
    mutationFn: () => {
      const body = Object.fromEntries(meta.data!.fields.filter((f) => !f.readOnly && f.name in values).map((f) => [f.name, values[f.name]]));
      return id ? put<{ _id: string }>(`/api/r/${key}/${encodeURIComponent(id)}`, body) : post<{ _id: string }>(`/api/r/${key}`, body);
    },
    onSuccess: async (res) => { await invalidateApp(app); navigate(`${base(app, key)}/${encodeURIComponent(res._id)}`); },
    onError: handleForbidden,
  });

  if (meta.error) return <ErrorBox error={meta.error} />;
  if (!meta.data || !ready) return <div className="panel"><Skeleton rows={10} cols={2} /></div>;
  const m = meta.data;
  const submit = (e: FormEvent) => { e.preventDefault(); save.mutate(); };
  const labels = (row.data?.row._labels ?? {}) as Record<string, Record<string, string>>;

  return (
    <form onSubmit={submit}>
      <p className="crumbs"><Link to={base(app, key)}>{m.title}</Link> › {id ? row.data?.row._label : `New ${m.name}`}</p>
      <h1>{id ? `Edit ${row.data?.row._label}` : `New ${m.name}`}</h1>
      <ErrorBox error={save.error} />
      <Panel>
        <div className="form-grid">
          {m.fields.filter((f) => !f.readOnly).map((f) => (
            <label key={f.name} className={f.type === 'text' || f.type === 'json' ? 'span-all' : f.type === 'bool' ? 'check' : ''}>
              <span>{f.label}{f.required && ' *'}</span>
              <Input field={f} value={values[f.name]} onChange={(v) => setValues((s) => ({ ...s, [f.name]: v }))} label={labels[f.name]?.[String(values[f.name])]} />
            </label>
          ))}
        </div>
      </Panel>
      <div className="row">
        <button className="btn" disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save'}</button>
        <Link className="btn outline" to={id ? `${base(app, key)}/${encodeURIComponent(id)}` : base(app, key)}>Cancel</Link>
      </div>
    </form>
  );
}

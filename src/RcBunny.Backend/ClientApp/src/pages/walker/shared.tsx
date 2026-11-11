import { api } from '../../api';

/* eslint-disable @typescript-eslint/no-explicit-any */

// ebunny's walker/_state, partials/report-actions and the run labels

const states: Record<string, [string, string]> = { queued: ['Queued', 'info'], running: ['Running', 'info'], done: ['Completed', 'ok'], failed: ['Error', 'bad'], stalled: ['Did not finish', 'warn'] };
export const StatePill = ({ state }: { state: string }) => <span className={`pill ${states[state]?.[1] ?? ''}`}>{states[state]?.[0] ?? state}</span>;
export const stateLabel = (s: string) => states[s]?.[0] ?? s;
export const when = (s: string) => new Date(s).toLocaleString('en-US', { month: 'numeric', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }).replace(',', '');
export const params = (p: Record<string, any> | null) => Object.entries(p ?? {}).map(([k, v]) => `${k.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())}: ${Array.isArray(v) ? v.join(', ') : v}`).join(' · ');

const save = <svg className="ri" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" /><path d="M17 21v-8H7v8M7 3v5h8" /></svg>;
const reload = <svg className="ri" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M21 12a9 9 0 1 1-2.64-6.36" /><path d="M21 3v6h-6" /></svg>;

/** ebunny's report-actions: a save icon (download) and a reload icon (run it again). size "page" shows labels. */
export function ReportActions({ download, label = 'Download', rerun, why = 'no file saved for this run', size = 'row' }:
  { download?: string | null; label?: string; rerun?: (() => void) | null; why?: string; size?: 'row' | 'page' }) {
  const page = size === 'page';
  return (
    <span className={`report-actions${page ? ' ra-page' : ''}`} onClick={(e) => e.stopPropagation()}>
      {download ? <a className={`btn ${page ? 'ghost' : 'sm ghost icon'}`} href={download} aria-label={label} data-tip={label}>{save}{page && <span>{label}</span>}</a>
        : <span className={`btn ${page ? 'ghost' : 'sm ghost icon'} disabled`} aria-disabled="true" role="img" aria-label={`Download unavailable: ${why}`} data-tip={`No download: ${why}`}>{save}{page && <span>{label}</span>}</span>}
      {rerun && <button className={`btn ${page ? 'cyan' : 'sm ghost icon'}`} aria-label="Rerun report" data-tip="Rerun report" onClick={rerun}>{reload}{page && <span>Rerun</span>}</button>}
    </span>
  );
}

/** The row actions for one run (ebunny's walker/_row-actions). */
export function RowActions({ run, onRerun }: { run: any; onRerun: (id: number) => void }) {
  return <ReportActions download={run.file ? `/api/walker/runs/${run.id}/download` : null} label={`Download ${run.format}`} rerun={run.rerun ? () => onRerun(run.id) : null}
    why={['queued', 'running'].includes(run.state) ? 'still running' : run.state === 'done' ? 'no rows to save' : 'the run did not finish'} />;
}

export const rerunRun = (id: number) => api.post<{ id: number; message: string }>(`/api/walker/runs/${id}/rerun`);

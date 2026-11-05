import { type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { DateInput } from '../../fields';
import { PageHead, useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Create Order / Create Order - Biz, or Renew / Change Plan with ?renew= an account number (ebunny's corral/order). */
export default function CreateOrder({ business }: { business: boolean }) {
  const [params] = useSearchParams();
  const renew = params.get('renew'), esiid = params.get('esiid');
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/orders/form?${qs({ biz: business, renew, esiid })}`);
  const [run, bar] = useRun();
  if (error) return <div className="flash bad">{error}</div>;
  if (!data) return <div className="empty">Loading…</div>;
  const p = data.prefill ?? {};
  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) ?? '';
    run(api.post<{ id: number; message: string }>('/api/orders', {
      renew: data.renew, biz: data.biz, businessName: v('business_name'), name: v('name'), phone: v('phone'), email: v('email'), address: v('address'), city: v('city'),
      zip: v('zip'), esiid: v('esiid'), marketId: Number(v('market_id')), planId: Number(v('plan_id')), enrollmentType: v('enrollment_type'), startDate: v('start_date') || null,
      autopay: f.get('autopay') === 'on', paperless: f.get('paperless') === 'on', peakPerks: f.get('peak_perks') === 'on', source: v('source'),
    }).then((r) => { navigate(`/corral/customers/${r.id}`); return r; }));
  };
  return (
    <>
      <PageHead title={data.renew ? 'Renew / Change Plan' : data.biz ? 'Create Order - Biz' : 'Create Order'}
        sub={data.renew ? <>For account <span className="mono">{data.renew}</span></> : `Enroll a new ${data.biz ? 'business' : 'residential'} customer over the phone.`} />
      {bar}
      <form className="panel simple-form" onSubmit={submit}><div className="panel-body">
        <div className="form-grid">
          {data.biz && <><label htmlFor="business_name">Business Name</label><input id="business_name" name="business_name" defaultValue={p.businessName ?? ''} required={!data.renew} /></>}
          <label htmlFor="name">{data.biz ? 'Contact Name' : 'Customer Name'}</label><input id="name" name="name" required defaultValue={p.name} />
          <label htmlFor="phone">Phone</label><input id="phone" name="phone" required defaultValue={p.phone} />
          <label htmlFor="email">Email</label><input id="email" name="email" type="email" required defaultValue={p.email} />
          <label htmlFor="address">Service Address</label><input id="address" name="address" required defaultValue={p.address} />
          <label htmlFor="city">City</label><input id="city" name="city" required defaultValue={p.city} />
          <label htmlFor="zip">Zip</label><input id="zip" name="zip" required maxLength={5} inputMode="numeric" defaultValue={p.zip} />
          <label htmlFor="esiid">ESIID</label><input id="esiid" name="esiid" className="mono" defaultValue={esiid ?? p.esiid ?? ''} placeholder="Leave blank to look up later" />
          <label htmlFor="market_id">Market</label>
          <select id="market_id" name="market_id" defaultValue={p.marketId}>{data.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select>
          <label htmlFor="plan_id">Plan</label>
          <select id="plan_id" name="plan_id" defaultValue={p.planId}>{data.plans.map((pl: any) => <option key={pl.id} value={pl.id}>{pl.name} — {pl.termMonths} mo ({pl.code})</option>)}</select>
          <label htmlFor="enrollment_type">Enrollment Type</label>
          <select id="enrollment_type" name="enrollment_type">{(data.renew ? ['Renewal'] : data.enrollmentTypes.filter((t: string) => t !== 'Renewal')).map((t: string) => <option key={t}>{t}</option>)}</select>
          <label htmlFor="start_date">Requested Start</label><DateInput id="start_date" name="start_date" min={new Date().toLocaleDateString('en-CA')} />
          <label>Options</label>
          <div className="actions">
            {[['autopay', 'AutoPay', p.autopay ?? true], ['paperless', 'Paperless', p.paperless ?? true], ['peak_perks', 'Peak Perks', p.peakPerks ?? false]].map(([k, label, on]) => (
              <label key={k} className="check"><input type="checkbox" name={k} defaultChecked={on} /> {label}</label>
            ))}
          </div>
          <label htmlFor="source">Source</label>
          <select id="source" name="source">{data.sources.map((s: string) => <option key={s}>{s}</option>)}</select>
        </div>
        <div className="actions" style={{ marginTop: 20 }}><button className="btn cyan">{data.renew ? 'Change Plan' : 'Submit Order'}</button>
          <Link className="btn ghost" to={p.customerId ? `/corral/customers/${p.customerId}` : '/corral'}>Cancel</Link></div>
      </div></form>
    </>
  );
}

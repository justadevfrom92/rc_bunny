import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, qs, useLoad } from '../../api';
import { useRun } from '../../ui';

/* eslint-disable @typescript-eslint/no-explicit-any */

/** Create Order (residential or business), or Renew / Change Plan when ?renew= an account number. */
export default function CreateOrder({ business }: { business: boolean }) {
  const [params] = useSearchParams();
  const renew = params.get('renew'), esiid = params.get('esiid');
  const navigate = useNavigate();
  const { data, error } = useLoad<any>(`/api/orders/form?${qs({ biz: business, renew, esiid })}`);
  const [marketId, setMarketId] = useState<number | null>(null);
  const [run, bar] = useRun();
  if (error) return <p className="error">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const p = data.prefill ?? {};
  const market = marketId ?? p.marketId ?? data.markets[0]?.id;

  const submit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget), v = (k: string) => (f.get(k) as string) ?? '';
    run(api.post<{ id: number; message: string }>('/api/orders', {
      renew: data.renew, biz: data.biz, businessName: v('businessName'), name: v('name'), phone: v('phone'), email: v('email'), address: v('address'), city: v('city'),
      zip: v('zip'), esiid: v('esiid'), marketId: Number(v('marketId')), planId: Number(v('planId')), enrollmentType: v('enrollmentType'), startDate: v('startDate') || null,
      autopay: f.get('autopay') === 'on', paperless: f.get('paperless') === 'on', peakPerks: f.get('peakPerks') === 'on', source: v('source'),
    }).then((r) => { navigate(`/corral/customers/${r.id}`); return r; }));
  };

  return (
    <>
      <h1>{data.renew ? `Renew / Change Plan · ${data.renew}` : `Create Order${data.biz ? ' - Biz' : ''}`}</h1>
      {data.renew && <p><Link to={`/corral/customers?q=${data.renew}`}>← Back to the account</Link></p>}
      {bar}
      <form className="card" onSubmit={submit}>
        {!data.renew && <>
          {data.biz && <label>Business name<input name="businessName" required /></label>}
          <div className="row">
            <label>{data.biz ? 'Contact name' : 'Customer name'}<input name="name" defaultValue={p.name} required /></label>
            <label>Phone<input name="phone" defaultValue={p.phone} required /></label>
            <label>Email<input name="email" type="email" defaultValue={p.email} required /></label>
          </div>
          <div className="row">
            <label>Service address<input name="address" defaultValue={p.address} required /></label>
            <label>City<input name="city" defaultValue={p.city} required /></label>
            <label>Zip<input name="zip" defaultValue={p.zip} maxLength={5} required /></label>
            <label>ESIID (if known)<input name="esiid" defaultValue={esiid ?? p.esiid ?? ''} /></label>
          </div>
        </>}
        <div className="row">
          <label>Market<select name="marketId" value={market} onChange={(e) => setMarketId(Number(e.target.value))}>{data.markets.map((m: any) => <option key={m.id} value={m.id}>{m.name}</option>)}</select></label>
          <label>Plan<select name="planId" defaultValue={p.planId} required>
            {data.plans.map((pl: any) => <option key={pl.id} value={pl.id} disabled={pl.prices[market] == null}>{pl.name} ({pl.code}) · {pl.termMonths <= 1 ? 'monthly' : `${pl.termMonths} mo`}{pl.prices[market] != null ? ` · ${pl.prices[market].toFixed(1)}¢ at 1,000 kWh` : ' · not sold in this market'}</option>)}
          </select></label>
        </div>
        <div className="row">
          <label>Enrollment type<select name="enrollmentType" defaultValue={data.renew ? 'Renewal' : 'Switch'}>{data.enrollmentTypes.map((t: string) => <option key={t}>{t}</option>)}</select></label>
          <label>Start date<input name="startDate" type="date" min={new Date().toISOString().slice(0, 10)} /></label>
          {!data.renew && <label>Source<select name="source">{data.sources.map((s: string) => <option key={s}>{s}</option>)}</select></label>}
        </div>
        {!data.renew && (
          <div className="row">
            <label style={{ flexDirection: 'row' }}><input type="checkbox" name="autopay" /> AutoPay</label>
            <label style={{ flexDirection: 'row' }}><input type="checkbox" name="paperless" defaultChecked /> Paperless billing</label>
            <label style={{ flexDirection: 'row' }}><input type="checkbox" name="peakPerks" /> Peak Perks</label>
          </div>
        )}
        <button className="primary">{data.renew ? 'Change plan' : 'Submit order'}</button>
      </form>
    </>
  );
}

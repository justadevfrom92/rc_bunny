import { Link } from 'react-router';
import { useAuth } from '../../auth';

/** An account number that links to Corral for people who can open it. */
export default function AccountLink({ id, account, name }: { id: number; account: string; name?: string }) {
  const { can } = useAuth();
  const text = <>{name ?? ''} <span className="muted">#{account}</span></>;
  return can('corral') ? <Link to={`/corral/customers/${id}`}>{text}</Link> : text;
}

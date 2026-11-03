import { Link } from 'react-router';
import { useAuth } from '../auth';

const tiles = [
  { to: '/corral', title: 'Corral', text: 'Customers, orders, texts and calls, CRM, products, ERCOT and exception queues.', permission: 'corral' },
  { to: '/plans', title: 'Plans & Rates', text: 'Current rates by market. Add new rates.', permission: 'plans' },
  { to: '/messages', title: 'Web Messages', text: 'Messages from the Contact Us page.', permission: 'messages' },
  { to: '/staff', title: 'Staff', text: 'Who can sign in and what each role can do.', permission: 'staff' },
];

export default function Home() {
  const { me, can } = useAuth();
  const mine = tiles.filter((t) => can(t.permission));
  return (
    <>
      <h1>Howdy, {me!.name.split(' ')[0]}</h1>
      {mine.length === 0 && <p className="notice">Your role doesn't open anything yet. Ask an administrator.</p>}
      <div className="tiles">
        {mine.map((t) => (
          <Link key={t.to} to={t.to} className="card tile">
            <h2>{t.title}</h2>
            <p>{t.text}</p>
          </Link>
        ))}
      </div>
    </>
  );
}

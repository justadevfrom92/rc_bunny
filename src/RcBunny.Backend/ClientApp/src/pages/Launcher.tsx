import { Link } from 'react-router-dom';
import { useMe } from '../auth';
import { useMeta } from '../queries';
import { Skeleton } from '../ui';

const icons: Record<string, string> = {
  users: '👥', ticket: '🎫', layout: '🧩', chart: '📈', file: '🗂️', train: '🚂', gift: '🎁', megaphone: '📣', bolt: '⚡', star: '⭐',
};

/** Every app: the ones you can open, and (greyed) the ones you can't, so people know what to ask for. */
export function Launcher() {
  const me = useMe();
  const { data: meta } = useMeta();
  if (!meta) return <div className="panel"><Skeleton rows={4} /></div>;

  return (
    <>
      <h1>Hi, {me.name.split(' ')[0]}</h1>
      <p className="muted">{me.company ? `${me.company} · ` : ''}{me.role}</p>
      <div className="tiles">
        {meta.apps.map((a) => {
          const open = me.apps.includes(a.key);
          const body = (
            <>
              <span className="tile-icon" aria-hidden="true">{icons[a.icon] ?? '•'}</span>
              <strong>{a.name}</strong>
              <span className="muted small">{a.description}</span>
              {!open && <span className="locked">No access</span>}
            </>
          );
          return open ? (
            <Link key={a.key} to={a.home} className="tile">{body}</Link>
          ) : (
            <div key={a.key} className="tile disabled" aria-disabled="true">{body}</div>
          );
        })}
      </div>
    </>
  );
}

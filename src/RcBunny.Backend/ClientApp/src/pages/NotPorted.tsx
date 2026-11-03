import { useLocation } from 'react-router-dom';
import { useMeta } from '../queries';

/** Screens that exist in ebunny but haven't been ported to C# yet. The route, menu and permission are already wired. */
export function NotPorted() {
  const { pathname } = useLocation();
  const { data: meta } = useMeta();
  const app = meta?.apps.find((a) => a.key === pathname.split('/')[1]);
  const item = app?.menu.flatMap((g) => g.items).find((i) => i.path.split('?')[0] === pathname);
  return (
    <div className="panel empty">
      <h2>{item?.label ?? app?.name ?? 'This screen'} isn't ported yet</h2>
      <p className="muted">
        It exists in the PHP version (ebunny). The menu link, route and {app ? <strong>{app.name}</strong> : 'app'} permission
        are already in place here; the screen goes in <code>ClientApp/src/pages/{app?.key ?? '…'}/</code> and its API in{' '}
        <code>Api/</code>.
      </p>
    </div>
  );
}

import { NavLink } from 'react-router';

export type Menu = { heading?: string; links: [label: string, path: string][] }[];

/** An app's left-hand menu. Paths are relative to the app, e.g. app="corral" + "customers" → /corral/customers. */
export default function Sidebar({ app, menu }: { app: string; menu: Menu }) {
  return (
    <nav className="sidebar">
      {menu.map((group, i) => (
        <div key={i}>
          {group.heading && <h3>{group.heading}</h3>}
          {group.links.map(([label, to]) => <NavLink key={to} to={`/${app}/${to}`} end>{label}</NavLink>)}
        </div>
      ))}
    </nav>
  );
}

import { Navigate, Route, Routes } from 'react-router';
import Sidebar, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Tickets from './Tickets';
import Ticket from './Ticket';
import Messages from './Messages';
import Team from './Team';
import Ratings from './Ratings';
import Employees from './Employees';

const menu: Menu = [
  { heading: 'Tickets', links: [['Dashboard', 'dashboard'], ['Tickets', 'tickets']] },
  { heading: 'Website', links: [['Web Messages', 'messages']] },
  { heading: 'Team', links: [['Ticket Team', 'team'], ['Approval Ratings', 'ratings'], ['Employees', 'employees']] },
];

export default function Ranger() {
  return (
    <div className="with-sidebar">
      <Sidebar app="ranger" menu={menu} />
      <div className="content">
        <Routes>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="tickets" element={<Tickets />} />
          <Route path="tickets/:id" element={<Ticket />} />
          <Route path="messages" element={<Messages />} />
          <Route path="team" element={<Team />} />
          <Route path="ratings" element={<Ratings />} />
          <Route path="employees" element={<Employees />} />
        </Routes>
      </div>
    </div>
  );
}

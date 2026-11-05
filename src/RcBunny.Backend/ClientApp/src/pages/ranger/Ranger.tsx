import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import Dashboard from './Dashboard';
import Tickets from './Tickets';
import Ticket from './Ticket';
import Attachment from './Attachment';
import Messages from './Messages';
import Message from './Message';
import Team from './Team';
import TeamAdd from './TeamAdd';
import Agent from './Agent';
import Satisfaction from './Satisfaction';
import Employees from './Employees';
import Employee from './Employee';

const menu: Menu = [
  { heading: 'Tickets', links: [['Dashboard', ''], ['Tickets', 'tickets', { badge: 'ranger_unassigned' }]] },
  { heading: 'Website', links: [['Web Messages', 'messages', { badge: 'messages' }]] },
  { heading: 'Team', links: [['Ticket Team', 'team'], ['Add a Team Member', 'team/add'], ['Approval Ratings', 'satisfaction'], ['Employees', 'employees']] },
];

export default function Ranger() {
  return (
    <AppShell app="ranger" menu={menu}>
        <Routes>
          <Route index element={<Dashboard />} />
          <Route path="tickets" element={<Tickets />} />
          <Route path="tickets/:id" element={<Ticket />} />
          <Route path="tickets/:id/files/:fileId" element={<Attachment />} />
          <Route path="messages" element={<Messages />} />
          <Route path="messages/:id" element={<Message />} />
          <Route path="team" element={<Team />} />
          <Route path="team/add" element={<TeamAdd />} />
          <Route path="team/:id" element={<Agent />} />
          <Route path="satisfaction" element={<Satisfaction />} />
          <Route path="employees" element={<Employees />} />
          <Route path="employees/:id" element={<Employee />} />
          <Route path="records/:type/:id" element={<Record />} />
        </Routes>
    </AppShell>
  );
}

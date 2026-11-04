import { Navigate, Route, Routes } from 'react-router';
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
  { heading: 'Tickets', links: [['Dashboard', 'dashboard'], ['Active', 'tickets?view=active'], ['Mine', 'tickets?view=mine'], ['Unassigned', 'tickets?view=unassigned'],
    ['Manager Requests', 'tickets?view=manager'], ['All Tickets', 'tickets?view=all']] },
  { heading: 'Website', links: [['Web Messages', 'messages']] },
  { heading: 'Team', links: [['Ticket Team', 'team'], ['Add Someone', 'team/add'], ['Satisfaction', 'satisfaction'], ['Employees', 'employees']] },
];

export default function Ranger() {
  return (
    <AppShell app="ranger" menu={menu}>
        <Routes>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="tickets" element={<Tickets />} />
          <Route path="tickets/:id" element={<Ticket />} />
          <Route path="tickets/:id/files/:fileId" element={<Attachment />} />
          <Route path="messages" element={<Messages />} />
          <Route path="messages/:id" element={<Message />} />
          <Route path="team" element={<Team />} />
          <Route path="team/add" element={<TeamAdd />} />
          <Route path="team/:id" element={<Agent />} />
          <Route path="satisfaction" element={<Satisfaction />} />
          <Route path="ratings" element={<Navigate to="../satisfaction" replace />} />
          <Route path="employees" element={<Employees />} />
          <Route path="employees/:id" element={<Employee />} />
        </Routes>
    </AppShell>
  );
}

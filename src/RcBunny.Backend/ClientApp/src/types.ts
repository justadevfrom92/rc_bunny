export interface Me {
  id: number;
  name: string;
  email: string;
  title: string | null;
  role: string | null;
  company: string | null;
  external: boolean;
  accessEndsOn: string | null;
  accessVersion: number;
  perms: string[];
  apps: string[];
  impersonatedBy: number | null;
}

export interface MenuItem {
  label: string;
  path: string;
  ported: boolean;
}
export interface MenuGroup {
  heading: string | null;
  items: MenuItem[];
}
export interface AdminApp {
  key: string;
  name: string;
  description: string;
  icon: string;
  home: string;
  menu: MenuGroup[];
}

export interface Meta {
  apps: AdminApp[];
  account: AdminApp;
  rights: Record<string, string>;
  departments: string[];
  corral: {
    statuses: Record<string, 'ok' | 'warn' | 'bad' | 'info'>;
    exceptions: string[];
    noteCategories: Record<string, string[]>;
    priorities: string[];
  };
}

export interface CustomerRow {
  id: number;
  account: string;
  name: string;
  type: string;
  phone: string | null;
  email: string | null;
  address: string;
  city: string;
  zip: string;
  esiid: string | null;
  market: string | null;
  plan: string | null;
  planCode: string | null;
  status: string;
  exception: string | null;
  balance: number;
  createdAt: string;
}

export interface SearchResult {
  rows: CustomerRow[];
  total: number;
  page: number;
  limit: number;
}

export interface Payment {
  id: number;
  reference: string;
  paidOn: string;
  amount: number;
  method: string;
  source: string;
  status: string;
  kind: string;
  confirmation: string | null;
  reversedAt: string | null;
}
export interface Bill {
  id: number;
  reference: string;
  billedOn: string;
  dueOn: string | null;
  kwh: number;
  amount: number;
  invoice: string | null;
}
export interface Note {
  id: number;
  author: string;
  body: string;
  category: string | null;
  action: string | null;
  priority: string | null;
  disposition: string | null;
  createdAt: string;
}

export interface CustomerDetail {
  customer: CustomerRow & {
    ticket: string;
    firstName: string | null;
    lastName: string | null;
    market: { name: string; description: string } | null;
    plan: { id: number; name: string; internal: string; term: number } | null;
    source: string;
    enrollmentType: string;
    startDate: string | null;
    dueDate: string | null;
    depositDue: number;
    autopay: boolean;
    paperless: boolean;
    peakPerks: boolean;
    marketingOptIn: boolean;
    stars: number;
    language: string;
    payments: Payment[];
    bills: Bill[];
    notes: Note[];
  };
  bookmarked: boolean;
  can: { reverse: boolean };
}

export interface StaffRow {
  id: number;
  name: string;
  email: string;
  employeeId: string | null;
  title: string | null;
  department: string | null;
  company: string | null;
  role: string | null;
  active: boolean;
  accessEndsOn: string | null;
  lastLoginAt: string | null;
  hasExceptions: boolean;
  perms: string[];
}

export interface StaffDetail {
  id: number;
  name: string;
  email: string;
  employeeId: string | null;
  title: string | null;
  department: string | null;
  company: string | null;
  accessEndsOn: string | null;
  phoneExt: string | null;
  location: string | null;
  hiredOn: string | null;
  active: boolean;
  lastLoginAt: string | null;
  roleId: number | null;
  role: string | null;
  rolePerms: string[];
  extraPerms: string[];
  deniedPerms: string[];
  accessNote: string | null;
  managerId: number | null;
  manager: string | null;
  perms: string[];
  reports: { id: number; name: string; title: string | null }[];
  recent: AuditEntry[];
  canLogInAs: boolean;
  cannotLogInAs: string | null;
}

export interface RoleRow {
  id: number;
  name: string;
  perms: string[];
  administrator: boolean;
  users: number;
}

export interface AuditEntry {
  id: number;
  userId: number | null;
  actor: string;
  action: string;
  subject: string | null;
  detail: string | null;
  ip: string | null;
  createdAt: string;
}

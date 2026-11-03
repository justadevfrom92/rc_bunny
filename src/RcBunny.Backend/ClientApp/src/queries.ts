import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api, qs, queryClient } from './api';
import type { AuditEntry, CustomerDetail, CustomerRow, Meta, RoleRow, SearchResult, StaffDetail, StaffRow } from './types';

/*
  Query keys start with the app key ('corral', 'sheriff', …) so the cache can drop everything for an app at once
  (when someone loses access to it, see auth.tsx). Stale times are set per kind of data:
    reference data (menus, statuses)  → 1 hour, it only changes on deploy
    lists                             → 30 s, plus previous page kept on screen while the next loads
    a single account                  → 60 s, prefetched when the row is hovered
*/

export const useMeta = () =>
  useQuery({ queryKey: ['meta'], queryFn: () => api<Meta>('/api/meta'), staleTime: 60 * 60_000, gcTime: Infinity });

export interface SearchFilters {
  q?: string;
  type?: string;
  status?: string;
  exception?: string;
  page?: number;
  limit?: number;
}

export const customerSearch = (f: SearchFilters) => ({
  queryKey: ['corral', 'search', f],
  queryFn: () => api<SearchResult>(`/api/corral/customers${qs({ ...f })}`),
  placeholderData: keepPreviousData,
});

export const useCorralStats = () =>
  useQuery({
    queryKey: ['corral', 'stats'],
    queryFn: () => api<{ accounts: number; onFlow: number; pending: number; exceptions: number }>('/api/corral/stats'),
    staleTime: 60_000,
  });

export const useBookmarks = () =>
  useQuery({ queryKey: ['corral', 'bookmarks'], queryFn: () => api<CustomerRow[]>('/api/corral/bookmarks') });

export const customerDetail = (id: number) => ({
  queryKey: ['corral', 'customer', id],
  queryFn: () => api<CustomerDetail>(`/api/corral/customers/${id}`),
  staleTime: 60_000,
});

/** Called on hover/focus of a search row so the account opens instantly. */
export const prefetchCustomer = (id: number) => queryClient.prefetchQuery(customerDetail(id));

export const useStaff = () =>
  useQuery({ queryKey: ['sheriff', 'staff'], queryFn: () => api<StaffRow[]>('/api/sheriff/users'), staleTime: 60_000 });

export const staffDetail = (id: number) => ({
  queryKey: ['sheriff', 'user', id],
  queryFn: () => api<StaffDetail>(`/api/sheriff/users/${id}`),
});

export const prefetchStaff = (id: number) => queryClient.prefetchQuery(staffDetail(id));

export const useRoles = () => useQuery({ queryKey: ['sheriff', 'roles'], queryFn: () => api<RoleRow[]>('/api/sheriff/roles') });

export const timeline = (q: string, userId?: number) => ({
  queryKey: ['sheriff', 'timeline', q, userId],
  queryFn: () => api<AuditEntry[]>(`/api/sheriff/timeline${qs({ q, userId })}`),
  placeholderData: keepPreviousData,
});

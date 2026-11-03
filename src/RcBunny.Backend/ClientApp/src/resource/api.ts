import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api, qs } from '../api';

export interface Field {
  name: string;
  prop: string;
  label: string;
  type: 'string' | 'text' | 'int' | 'decimal' | 'bool' | 'date' | 'datetime' | 'time' | 'json' | 'list' | 'ref';
  required: boolean;
  readOnly: boolean;
  maxLength: number | null;
  options: string[] | null;
  ref: string | null; // referenced table
}

export interface ActionMeta {
  key: string;
  label: string;
  confirm: string | null;
  inputs: Record<string, string>;
  tone: string;
}

export interface ResourceMeta {
  key: string;
  app: string;
  title: string;
  name: string;
  description: string | null;
  list: string[];
  search: boolean;
  filters: string[];
  sort: string;
  fields: Field[];
  create: boolean;
  edit: boolean;
  delete: boolean;
  actions: ActionMeta[];
  children: { title: string; resource: string; foreignKey: string }[];
}

export type Row = Record<string, unknown> & { _id: string; _label: string };

export interface ListResult {
  rows: Row[];
  total: number;
  page: number;
  limit: number;
  labels: Record<string, Record<string, string>>;
}

/*
  Cache keys start with the resource's app ('corral', 'rodeo', …) like every other query, so losing access to an app
  drops its data (auth.tsx). Metadata only changes on deploy: cached for the session.
*/
export const metaQuery = (key: string) => ({
  queryKey: ['resource-meta', key],
  queryFn: () => api<ResourceMeta>(`/api/r/${key}/meta`),
  staleTime: 30 * 60_000,
});

export const useMeta = (key: string) => useQuery(metaQuery(key));

export const useList = (app: string, key: string, params: Record<string, string | number | undefined>) =>
  useQuery({
    queryKey: [app, 'r', key, 'list', params],
    queryFn: () => api<ListResult>(`/api/r/${key}${qs(params)}`),
    placeholderData: keepPreviousData,
  });

export const rowQuery = (app: string, key: string, id: string) => ({
  queryKey: [app, 'r', key, 'row', id],
  queryFn: () => api<{ row: Row & { _labels: Record<string, Record<string, string>> }; actions: string[] }>(`/api/r/${key}/${encodeURIComponent(id)}`),
});

export const useOptions = (table: string | null, q: string) =>
  useQuery({
    queryKey: ['options', table, q],
    queryFn: () => api<{ key: string; value: string }[]>(`/api/r/options/${table}${qs({ q })}`),
    enabled: !!table,
    staleTime: 5 * 60_000,
    placeholderData: keepPreviousData,
  });

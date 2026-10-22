import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { api } from '../shared/api';
import type {
  AuditEntry, Category, Dashboard, MediaItem, MediaUsage, Me, NewsDetail, NewsListItem, Paged, Schemas, SectionDetail,
  SectionSummary, SettingsDetail, Submission, User,
} from './types';

/** Query keys and data hooks for every dashboard screen. All data comes from the API; nothing is cached across sign-ins. */
export const keys = {
  me: ['me'] as const,
  dashboard: ['dashboard'] as const,
  schemas: ['schemas'] as const,
  sections: ['sections'] as const,
  section: (key: string) => ['sections', key] as const,
  settings: ['settings'] as const,
  news: (params: Record<string, string | number>) => ['news', 'list', params] as const,
  newsAll: ['news'] as const,
  article: (id: number) => ['news', 'item', id] as const,
  categories: ['categories'] as const,
  media: (params: Record<string, string | number>) => ['media', 'list', params] as const,
  mediaAll: ['media'] as const,
  mediaItem: (id: number) => ['media', 'item', id] as const,
  usage: (id: number) => ['media', 'usage', id] as const,
  messages: (params: Record<string, string | number>) => ['messages', 'list', params] as const,
  messagesAll: ['messages'] as const,
  message: (id: number) => ['messages', 'item', id] as const,
  users: ['users'] as const,
  audit: (params: Record<string, string | number>) => ['audit', params] as const,
};

export function qs(params: Record<string, string | number | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== '' && v !== null && v !== undefined) search.set(k, String(v));
  }
  const s = search.toString();
  return s ? `?${s}` : '';
}

export const useMe = () =>
  useQuery({ queryKey: keys.me, queryFn: () => api<Me>('/api/auth/me'), retry: false, staleTime: 5 * 60_000 });

export const useDashboard = () => useQuery({ queryKey: keys.dashboard, queryFn: () => api<Dashboard>('/api/admin/dashboard'), refetchInterval: 60_000 });

export const useSchemas = () => useQuery({ queryKey: keys.schemas, queryFn: () => api<Schemas>('/api/admin/schemas'), staleTime: Infinity });

export const useSections = () => useQuery({ queryKey: keys.sections, queryFn: () => api<SectionSummary[]>('/api/admin/sections') });

export const useSection = (key: string) =>
  useQuery({ queryKey: keys.section(key), queryFn: () => api<SectionDetail>(`/api/admin/sections/${encodeURIComponent(key)}`) });

export const useSettings = () => useQuery({ queryKey: keys.settings, queryFn: () => api<SettingsDetail>('/api/admin/settings') });

export const useNewsList = (params: Record<string, string | number>) =>
  useQuery({ queryKey: keys.news(params), queryFn: () => api<Paged<NewsListItem>>(`/api/admin/news${qs(params)}`), placeholderData: keepPreviousData });

export const useArticle = (id: number | null) =>
  useQuery({ queryKey: keys.article(id ?? 0), queryFn: () => api<NewsDetail>(`/api/admin/news/${id}`), enabled: id !== null });

export const useCategories = () => useQuery({ queryKey: keys.categories, queryFn: () => api<Category[]>('/api/admin/news-categories') });

export const useMediaList = (params: Record<string, string | number>) =>
  useQuery({ queryKey: keys.media(params), queryFn: () => api<Paged<MediaItem>>(`/api/admin/media${qs(params)}`), placeholderData: keepPreviousData });

export const useMediaItem = (id: number | null) =>
  useQuery({ queryKey: keys.mediaItem(id ?? 0), queryFn: () => api<MediaItem>(`/api/admin/media/${id}`), enabled: id !== null && id > 0 });

export const useMediaUsage = (id: number | null) =>
  useQuery({ queryKey: keys.usage(id ?? 0), queryFn: () => api<MediaUsage[]>(`/api/admin/media/${id}/usage`), enabled: id !== null && id > 0 });

export const useMessages = (params: Record<string, string | number>) =>
  useQuery({ queryKey: keys.messages(params), queryFn: () => api<Paged<Submission>>(`/api/admin/messages${qs(params)}`), placeholderData: keepPreviousData, refetchInterval: 60_000 });

export const useMessage = (id: number | null) =>
  useQuery({ queryKey: keys.message(id ?? 0), queryFn: () => api<Submission>(`/api/admin/messages/${id}`), enabled: id !== null });

export const useUsers = () => useQuery({ queryKey: keys.users, queryFn: () => api<User[]>('/api/admin/users') });

export const useAudit = (params: Record<string, string | number>) =>
  useQuery({ queryKey: keys.audit(params), queryFn: () => api<Paged<AuditEntry>>(`/api/admin/audit${qs(params)}`), placeholderData: keepPreviousData });

/** A mutation that refreshes the given queries when it succeeds. */
export function useApiMutation<TVars, TResult = unknown>(
  fn: (vars: TVars) => Promise<TResult>,
  invalidate: (vars: TVars, result: TResult) => QueryKey[] = () => [],
) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: async (result, vars) => {
      await Promise.all(invalidate(vars, result).map((queryKey) => client.invalidateQueries({ queryKey })));
    },
  });
}

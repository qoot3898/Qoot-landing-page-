import { StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom';
import { ApiError } from '../shared/api';
import './admin.css';
import { I18nProvider } from './i18n';
import { ConfirmProvider, ToastProvider } from './ui/feedback';
import { AuthGate, LoginPage, RoleGate } from './auth';
import { Shell } from './Shell';
import { OverviewPage } from './pages/Overview';
import { SectionsPage } from './pages/Sections';
import { SectionEditorPage } from './pages/SectionEditor';
import { NewsPage } from './pages/News';
import { NewsEditorPage } from './pages/NewsEditor';
import { CategoriesPage } from './pages/Categories';
import { MediaPage } from './pages/Media';
import { MessagesPage } from './pages/Messages';
import { SettingsPage } from './pages/Settings';
import { UsersPage } from './pages/Users';
import { AuditPage } from './pages/Audit';
import { AccountPage } from './pages/Account';
import { NotFoundPage } from './pages/NotFound';

/** A signed-out session (expired, or ended elsewhere) sends the user to the sign-in page, then back. */
function onAuthError(error: unknown, quiet: boolean) {
  if (!(error instanceof ApiError) || error.status !== 401 || quiet) return;
  if (location.pathname.startsWith('/admin/login')) return;
  const next = location.pathname + location.search;
  location.assign(`/admin/login?expired=1&next=${encodeURIComponent(next)}`);
}

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: (error, query) => onAuthError(error, query.meta?.quiet401 === true) }),
  mutationCache: new MutationCache({ onError: (error) => onAuthError(error, false) }),
  defaultOptions: {
    queries: {
      staleTime: 20_000,
      refetchOnWindowFocus: true,
      retry: (count, error) => !(error instanceof ApiError) || (error.status >= 500 && count < 2) || (error.isNetwork && count < 2),
    },
  },
});

const router = createBrowserRouter(
  [
    { path: '/login', element: <LoginPage /> },
    {
      element: <AuthGate />,
      children: [
        {
          element: <Shell />,
          children: [
            { index: true, element: <OverviewPage /> },
            { path: 'sections', element: <SectionsPage /> },
            { path: 'sections/:key', element: <SectionEditorPage /> },
            { path: 'news', element: <NewsPage /> },
            { path: 'news/new', element: <NewsEditorPage /> },
            { path: 'news/categories', element: <CategoriesPage /> },
            { path: 'news/:id', element: <NewsEditorPage /> },
            { path: 'media', element: <MediaPage /> },
            { path: 'messages', element: <MessagesPage /> },
            { path: 'messages/:id', element: <MessagesPage /> },
            { path: 'settings', element: <SettingsPage /> },
            { path: 'users', element: <RoleGate role="Admin"><UsersPage /></RoleGate> },
            { path: 'audit', element: <RoleGate role="Admin"><AuditPage /></RoleGate> },
            { path: 'account', element: <AccountPage /> },
            { path: 'index.html', element: <Navigate to="/" replace /> },
            { path: '*', element: <NotFoundPage /> },
          ],
        },
      ],
    },
  ],
  { basename: '/admin' },
);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <ConfirmProvider>
            <Suspense>
              <RouterProvider router={router} />
            </Suspense>
          </ConfirmProvider>
        </ToastProvider>
      </QueryClientProvider>
    </I18nProvider>
  </StrictMode>,
);

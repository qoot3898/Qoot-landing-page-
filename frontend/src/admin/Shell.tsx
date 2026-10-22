import { useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useOutletContext } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../shared/api';
import { useCurrentUser, useSignOut, type AuthContext } from './auth';
import { keys, useDashboard } from './data';
import { useI18n } from './i18n';
import { Icon, Wordmark, type IconName } from './ui/icons';

interface NavItem { to: string; label: string; icon: IconName; count?: number; end?: boolean }

/**
 * The frame: a deep-green sidebar with the cream wordmark and the band of Qoot's food drawings (as
 * on the website footer), beside the cream work area. On a phone it folds into a green bar and a
 * menu sheet.
 */
export function Shell() {
  const me = useCurrentUser();
  const { t, lang, setLang } = useI18n();
  const signOut = useSignOut();
  const location = useLocation();
  const client = useQueryClient();
  const dashboard = useDashboard();
  const [menuOpen, setMenuOpen] = useState(false);
  const isAdmin = me.role === 'Admin';

  useEffect(() => setMenuOpen(false), [location.pathname]);
  useEffect(() => {
    document.body.classList.toggle('menu-open', menuOpen);
  }, [menuOpen]);

  const newRequests = dashboard.data?.messages.new ?? 0;
  const unpublished = dashboard.data?.sections.withUnpublishedChanges ?? 0;

  const content: NavItem[] = [
    { to: '/', label: t.nav.overview, icon: 'overview', end: true },
    { to: '/messages', label: t.nav.messages, icon: 'messages', count: newRequests },
    { to: '/sections', label: t.nav.sections, icon: 'sections' },
    { to: '/news', label: t.nav.news, icon: 'news' },
    { to: '/media', label: t.nav.media, icon: 'media' },
  ];
  const admin: NavItem[] = [
    { to: '/settings', label: t.nav.settings, icon: 'settings' },
    ...(isAdmin
      ? [
          { to: '/users', label: t.nav.users, icon: 'users' as const },
          { to: '/audit', label: t.nav.audit, icon: 'audit' as const },
        ]
      : []),
  ];

  const switchLanguage = () => {
    const next = lang === 'ar' ? 'en' : 'ar';
    setLang(next);
    // Remember it on the account too, so it follows the user to other devices.
    void api('/api/auth/profile', { method: 'PUT', body: { displayName: me.displayName, preferredLanguage: next }, lang: next })
      .then((updated) => client.setQueryData(keys.me, updated))
      .catch(() => undefined);
  };

  const siteHome = lang === 'ar' ? '/' : '/en/';
  const nav = (
    <nav className="nav" aria-label={t.nav.menu}>
      <p className="nav-group">{t.nav.groupContent}</p>
      <ul>{content.map((item) => <NavRow key={item.to} item={item} />)}</ul>
      <p className="nav-group">{t.nav.groupAdmin}</p>
      <ul>{admin.map((item) => <NavRow key={item.to} item={item} />)}</ul>
    </nav>
  );

  const userRow = (
    <div className="user-row">
      <Link to="/account" className="user-link">
        <span className="avatar" aria-hidden="true">{initials(me.displayName || me.email)}</span>
        <span className="user-text">
          <span className="user-name">{me.displayName || me.email}</span>
          <span className="user-role">{lang === 'ar' ? (me.role === 'Admin' ? 'مسؤول' : 'محرر') : me.role === 'Admin' ? 'Administrator' : 'Editor'}</span>
        </span>
      </Link>
      <button type="button" className="icon-btn" onClick={() => void signOut()} aria-label={t.top.signOut} title={t.top.signOut}>
        <Icon name="signOut" />
      </button>
    </div>
  );

  return (
    <div className="app">
      <a className="skip" href="#work">{t.skip}</a>
      <aside className="sidebar">
        <div className="sidebar-scroll">
          <Link to="/" className="brand">
            <Wordmark lang={lang} height={40} alt={t.product} />
            <span className="brand-text">
              <span className="brand-name">{t.productSub}</span>
              <span className="brand-scope">{t.productScope}</span>
            </span>
          </Link>
          {nav}
        </div>
        <div className="sidebar-foot">
          <div className="sidebar-band" aria-hidden="true"><span /></div>
          {userRow}
        </div>
      </aside>

      <header className="topbar">
        <button type="button" className="icon-btn menu-toggle" aria-expanded={menuOpen} aria-controls="phone-menu" onClick={() => setMenuOpen((v) => !v)}>
          <Icon name={menuOpen ? 'close' : 'menu'} />
          <span className="sr-only">{menuOpen ? t.nav.closeMenu : t.nav.menu}</span>
        </button>
        <Link to="/" className="brand brand-phone">
          <Wordmark lang={lang} height={30} alt={t.product} />
        </Link>
        {unpublished > 0 && (
          <Link to="/sections" className="top-note">
            <span className="dot" aria-hidden="true" />
            {t.top.unpublished(unpublished)}
          </Link>
        )}
        <div className="top-actions">
          <button type="button" className="btn btn-sm btn-quiet" onClick={switchLanguage} lang={lang === 'ar' ? 'en' : 'ar'} aria-label={`${t.top.languageLabel}: ${t.top.language}`}>
            <Icon name="globe" />
            <span className="hide-phone">{t.top.language}</span>
          </button>
          <a className="btn btn-sm" href={siteHome} target="_blank" rel="noopener">
            <span className="hide-phone">{t.top.viewSite}</span>
            <Icon name="external" />
            <span className="sr-only">{t.common.open}</span>
          </a>
        </div>
      </header>

      <div id="phone-menu" className="phone-menu" hidden={!menuOpen}>
        {nav}
        {userRow}
      </div>

      <main id="work" className="sheet" tabIndex={-1}>
        <Outlet context={{ me } satisfies AuthContext} />
      </main>
    </div>
  );
}

function NavRow({ item }: { item: NavItem }) {
  return (
    <li>
      <NavLink to={item.to} end={item.end} className="nav-row">
        <Icon name={item.icon} />
        <span className="nav-label">{item.label}</span>
        {item.count ? <span className="nav-count num">{item.count}</span> : null}
      </NavLink>
    </li>
  );
}

function initials(name: string): string {
  const parts = name.replace(/@.*/, '').split(/[\s._-]+/).filter(Boolean);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

export function useShellUser(): AuthContext['me'] {
  return useOutletContext<AuthContext>().me;
}

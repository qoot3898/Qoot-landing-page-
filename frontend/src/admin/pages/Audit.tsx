import { useEffect, useState } from 'react';
import { useAudit } from '../data';
import { useI18n } from '../i18n';
import { Empty, ErrorBlock, PageHead, Pager, SearchBox, Select, SkeletonRows } from '../ui/controls';

const KINDS = ['all', 'auth', 'section', 'news', 'media', 'settings', 'user', 'message'] as const;

/** Every sign-in and every change, newest first. Administrators only (enforced by the API). */
export function AuditPage() {
  const { t, date } = useI18n();
  const [kind, setKind] = useState<(typeof KINDS)[number]>('all');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  useEffect(() => {
    const id = window.setTimeout(() => { setQuery(search); setPage(1); }, 300);
    return () => window.clearTimeout(id);
  }, [search]);
  const log = useAudit({ page, pageSize: 50, ...(kind !== 'all' ? { action: kind } : {}), ...(query ? { search: query } : {}) });

  return (
    <div className="page">
      <PageHead title={t.audit.title} lead={t.audit.lead} />
      <div className="toolbar">
        <SearchBox value={search} onChange={setSearch} placeholder={t.audit.searchPlaceholder} />
        <Select ariaLabel={t.audit.filter} value={kind} onChange={(v) => { setKind(v as (typeof KINDS)[number]); setPage(1); }} options={KINDS.map((k) => ({ value: k, label: t.audit.kinds[k] }))} />
        <span className="toolbar-end">{log.data && <span className="muted num">{t.audit.count(log.data.total)}</span>}</span>
      </div>
      {log.isPending ? (
        <SkeletonRows rows={10} />
      ) : log.error ? (
        <ErrorBlock error={log.error} onRetry={() => void log.refetch()} />
      ) : log.data!.items.length === 0 ? (
        <Empty>{t.audit.empty}</Empty>
      ) : (
        <>
          <table className="table table-dense">
            <thead>
              <tr>
                <th scope="col" className="num">{t.audit.when}</th>
                <th scope="col" className="col-id">{t.audit.what}</th>
                <th scope="col">{t.audit.who}</th>
                <th scope="col" className="hide-tablet">{t.audit.from}</th>
              </tr>
            </thead>
            <tbody>
              {log.data!.items.map((a) => (
                <tr key={a.id}>
                  <td className="num nowrap">{date(a.at, true)}</td>
                  <td className="col-id">
                    <span className="cell-text">
                      <span dir="auto">{a.summary}</span>
                      <code className="cell-sub" dir="ltr">{a.action}</code>
                    </span>
                  </td>
                  <td dir="ltr" className="cell-email">{a.userEmail || t.common.dash}</td>
                  <td className="hide-tablet"><code dir="ltr">{a.ipAddress ?? t.common.dash}</code></td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pager page={log.data!.page} totalPages={log.data!.totalPages} total={log.data!.total} pageSize={log.data!.pageSize} onPage={setPage} />
        </>
      )}
    </div>
  );
}

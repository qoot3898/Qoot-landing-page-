import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../shared/api';
import { keys, useMessage, useMessages } from '../data';
import { useI18n } from '../i18n';
import type { Submission, SubmissionStatus } from '../types';
import { Empty, ErrorBlock, errorText, Field, PageHead, Pager, SearchBox, SkeletonRows, Tabs, TextArea } from '../ui/controls';
import { useConfirm, useToast } from '../ui/feedback';
import { Icon } from '../ui/icons';
import { MessageStatusPill } from '../ui/status';

type Tab = 'open' | 'new' | 'in_progress' | 'done' | 'archived' | 'all';
const STATUSES: SubmissionStatus[] = ['new', 'in_progress', 'done', 'archived'];

/** The Contact us messages sent from the website: a queue on one side, the message and what to do with it on the other. */
export function MessagesPage() {
  const { t, relative } = useI18n();
  const params = useParams();
  const selected = params.id ? Number(params.id) : null;
  const [search, setSearch] = useSearchParams();
  const tab = (search.get('status') as Tab | null) ?? 'open';
  const page = Number(search.get('page')) || 1;
  const [text, setText] = useState(search.get('q') ?? '');


  const setParam = (changes: Record<string, string>) =>
    setSearch(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [k, v] of Object.entries(changes)) {
          if (v) next.set(k, v);
          else next.delete(k);
        }
        return next;
      },
      { replace: true },
    );

  useEffect(() => {
    if (text === (search.get('q') ?? '')) return;
    const id = window.setTimeout(() => setParam({ q: text, page: '' }), 300);
    return () => window.clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text]);

  const list = useMessages({ page, pageSize: 25, ...(tab !== 'all' ? { status: tab } : {}), ...(search.get('q') ? { search: search.get('q')! } : {}) });
  const query = search.toString() ? `?${search.toString()}` : '';

  return (
    <div className={`page inbox${selected ? ' has-selection' : ''}`}>
      <PageHead title={t.messages.title} lead={t.messages.lead} />
      <Tabs<Tab>
        label={t.messages.title}
        value={tab}
        onChange={(v) => setParam({ status: v === 'open' ? '' : v, page: '' })}
        items={(['open', 'new', 'in_progress', 'done', 'archived', 'all'] as Tab[]).map((v) => ({ value: v, label: t.messages.tabs[v] }))}
      />
      <div className="toolbar">
        <SearchBox value={text} onChange={setText} placeholder={t.messages.searchPlaceholder} />
        <span className="toolbar-end">{list.data && <span className="muted num">{t.messages.count(list.data.total)}</span>}</span>
      </div>
      <div className="inbox-panes">
        <div className="inbox-list">
          {list.isPending ? (
            <SkeletonRows rows={6} />
          ) : list.error ? (
            <ErrorBlock error={list.error} onRetry={() => void list.refetch()} />
          ) : list.data!.items.length === 0 ? (
            <Empty>{tab === 'open' && !search.get('q') ? t.messages.emptyOpen : t.messages.empty}</Empty>
          ) : (
            <>
              <ul className="queue">
                {list.data!.items.map((m) => (
                  <li key={m.id}>
                    <Link to={`/messages/${m.id}${query}`} className={`queue-row${m.id === selected ? ' is-selected' : ''}${m.isRead ? '' : ' is-unread'}`} aria-current={m.id === selected ? 'true' : undefined}>
                      <Initials name={m.name} />
                      <span className="queue-top">
                        <span className="queue-name" dir="auto">
                          {!m.isRead && <span className="unread-dot" aria-label={t.status.unread} />}
                          {m.name}
                        </span>
                        <span className="muted small nowrap">{relative(m.createdAt)}</span>
                      </span>
                      <span className="queue-org muted small" dir="ltr">{[m.phone, m.email].filter(Boolean).join(' · ')}</span>
                      <span className="queue-bottom">
                        <span className="queue-need" dir="auto">{m.message}</span>
                        <MessageStatusPill status={m.status} />
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
              <Pager page={list.data!.page} totalPages={list.data!.totalPages} total={list.data!.total} pageSize={list.data!.pageSize} onPage={(p) => setParam({ page: p > 1 ? String(p) : '' })} />
            </>
          )}
        </div>
        <div className="inbox-detail">
          {selected ? <MessageDetail id={selected} query={query} /> : <Empty>{t.messages.pick}</Empty>}
        </div>
      </div>
    </div>
  );
}

function MessageDetail({ id, query }: { id: number; query: string }) {
  const { t, date } = useI18n();
  const message = useMessage(id);
  const client = useQueryClient();
  const toast = useToast();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const m = message.data;

  useEffect(() => {
    if (m) {
      setNotes(m.adminNotes);
      // Opening a request marks it read on the server: refresh the queue and the counts.
      void client.invalidateQueries({ queryKey: ['messages', 'list'] });
      void client.invalidateQueries({ queryKey: keys.dashboard });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [m?.id]);

  if (message.isPending) return <SkeletonRows rows={6} />;
  if (message.error instanceof ApiError && message.error.status === 404) return <Empty>{t.common.notFoundText}</Empty>;
  if (message.error || !m) return <ErrorBlock error={message.error} onRetry={() => void message.refetch()} />;

  const patch = async (body: Partial<Pick<Submission, 'status' | 'adminNotes' | 'isRead'>>) => {
    setBusy(true);
    try {
      const fresh = await api<Submission>(`/api/admin/messages/${id}`, { method: 'PATCH', body });
      client.setQueryData(keys.message(id), fresh);
      void client.invalidateQueries({ queryKey: ['messages', 'list'] });
      void client.invalidateQueries({ queryKey: keys.dashboard });
      toast.show(t.messages.updated);
    } catch (e) {
      toast.show(errorText(e, t), 'error');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!(await confirm({ title: t.messages.deleteConfirm(m.name), confirm: t.common.delete, danger: true }))) return;
    try {
      await api(`/api/admin/messages/${id}`, { method: 'DELETE' });
      client.removeQueries({ queryKey: keys.message(id) });
      void client.invalidateQueries({ queryKey: keys.messagesAll });
      void client.invalidateQueries({ queryKey: keys.dashboard });
      toast.show(t.messages.deleted);
      navigate(`/messages${query}`, { replace: true });
    } catch (e) {
      toast.show(errorText(e, t), 'error');
    }
  };

  const digits = m.phone.replace(/[^\d+]/g, '');
  const wa = digits.replace(/^\+/, '').replace(/^00/, '').replace(/^0/, '218');

  return (
    <article className="message" aria-labelledby="message-title">
      <Link to={`/messages${query}`} className="crumb phone-only"><Icon name="chevron" className="flip" /> {t.messages.title}</Link>
      <header className="message-head">
        <div className="message-who">
          <Initials name={m.name} />
          <div>
            <h2 id="message-title" dir="auto">{m.name}</h2>
            <p className="muted small num">{date(m.createdAt, true)} · {m.language === 'ar' ? t.common.arabic : t.common.english}</p>
          </div>
        </div>
        <MessageStatusPill status={m.status} />
      </header>

      <div className="contact-cards">
        <div className="contact-card">
          <span className="label">{t.messages.phone}</span>
          <span className="contact-value num" dir="ltr">{m.phone || t.common.dash}</span>
          {digits && (
            <span className="actions">
              <a className="btn btn-sm btn-primary" href={`tel:${digits}`}><Icon name="phone" /> {t.messages.call}</a>
              <a className="btn btn-sm" href={`https://wa.me/${wa}`} target="_blank" rel="noopener noreferrer"><Icon name="chat" /> {t.messages.whatsapp}</a>
            </span>
          )}
        </div>
        <div className="contact-card">
          <span className="label">{t.messages.emailAddress}</span>
          {m.email ? (
            <>
              <span className="contact-value" dir="ltr">{m.email}</span>
              <span className="actions">
                <a className="btn btn-sm" href={`mailto:${m.email}`}><Icon name="mail" /> {t.messages.reply}</a>
              </span>
            </>
          ) : (
            <span className="muted">{t.messages.noEmail}</span>
          )}
        </div>
      </div>

      <section className="message-text">
        <h3>{t.messages.message}</h3>
        <p dir="auto">{m.message}</p>
      </section>

      <fieldset className="field">
        <legend className="field-label">{t.messages.status}</legend>
        <div className="segmented" role="radiogroup" aria-label={t.messages.status}>
          {STATUSES.map((s) => (
            <button key={s} type="button" role="radio" aria-checked={m.status === s} className="segment" disabled={busy} onClick={() => m.status !== s && void patch({ status: s })}>
              {t.status[s]}
            </button>
          ))}
        </div>
      </fieldset>

      <Field label={t.messages.notes} help={t.messages.notesHelp} htmlFor="message-notes">
        <TextArea id="message-notes" value={notes} dir="auto" maxLength={4000} onChange={(e) => setNotes(e.target.value)} />
      </Field>
      <div className="message-foot">
        <button type="button" className="btn" disabled={busy || notes === m.adminNotes} onClick={() => void patch({ adminNotes: notes })}>{t.messages.saveNotes}</button>
        <button type="button" className="btn btn-quiet" disabled={busy} onClick={() => void patch({ isRead: false })}>{t.messages.markUnread}</button>
        <button type="button" className="btn btn-quiet danger push-end" onClick={() => void remove()}><Icon name="trash" /> {t.common.delete}</button>
      </div>
    </article>
  );
}

/** A person's initials in a pale tint of their own (stable per name), so the same sender is recognised in the queue. */
export function Initials({ name }: { name: string }) {
  const words = name.trim().split(/\s+/).filter(Boolean);
  // Arabic letters joined together read as a word, so an Arabic name shows its first letter only.
  const arabic = /[\u0600-\u06FF]/.test(name);
  const letters = words.slice(0, arabic ? 1 : 2).map((w) => [...w][0] ?? '').join('').toUpperCase() || '?';
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + (ch.codePointAt(0) ?? 0)) >>> 0;
  return <span className="initials" data-tone={hash % 5} aria-hidden="true">{letters}</span>;
}

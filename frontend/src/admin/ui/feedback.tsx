import { createContext, useCallback, useContext, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { useI18n } from '../i18n';
import { Icon } from './icons';

// ------------------------------------------------------------------ toasts

type ToastKind = 'ok' | 'error' | 'info';
interface Toast { id: number; kind: ToastKind; text: string; action?: { label: string; run: () => void } }
interface ToastApi { show: (text: string, kind?: ToastKind, action?: Toast['action']) => void }

const ToastCtx = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const next = useRef(1);
  const { t } = useI18n();

  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((x) => x.id !== id)), []);
  const show = useCallback<ToastApi['show']>(
    (text, kind = 'ok', action) => {
      const id = next.current++;
      setToasts((list) => [...list.slice(-3), { id, kind, text, action }]);
      window.setTimeout(() => dismiss(id), kind === 'error' ? 9000 : 4500);
    },
    [dismiss],
  );

  return (
    <ToastCtx.Provider value={{ show }}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.kind}`} role={toast.kind === 'error' ? 'alert' : undefined}>
            <Icon name={toast.kind === 'error' ? 'alert' : toast.kind === 'ok' ? 'check' : 'info'} />
            <span>{toast.text}</span>
            {toast.action && (
              <button type="button" className="link" onClick={() => { toast.action!.run(); dismiss(toast.id); }}>
                {toast.action.label}
              </button>
            )}
            <button type="button" className="icon-btn" onClick={() => dismiss(toast.id)} aria-label={t.common.close}>
              <Icon name="close" size={14} />
            </button>
          </div>
        ))}
      </div>
    </ToastCtx.Provider>
  );
}

export function useToast(): ToastApi {
  const value = useContext(ToastCtx);
  if (!value) throw new Error('useToast outside ToastProvider');
  return value;
}

// ------------------------------------------------------------------ dialogs

/** A modal built on <dialog>: focus is trapped and Escape closes it, natively. */
export function Dialog({ open, onClose, title, children, wide = false, footer }: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  wide?: boolean;
  footer?: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (open && !el.open) el.showModal();
    if (!open && el.open) el.close();
  }, [open]);
  return (
    <dialog
      ref={ref}
      className={wide ? 'dialog dialog-wide' : 'dialog'}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={(e) => { e.preventDefault(); onClose(); }}
      onClick={(e) => { if (e.target === ref.current) onClose(); }}
    >
      {open && (
        <div className="dialog-inner">
          <header className="dialog-head">
            <h2 id={titleId}>{title}</h2>
            <CloseButton onClick={onClose} />
          </header>
          <div className="dialog-body">{children}</div>
          {footer && <footer className="dialog-foot">{footer}</footer>}
        </div>
      )}
    </dialog>
  );
}

function CloseButton({ onClick }: { onClick: () => void }) {
  const { t } = useI18n();
  return (
    <button type="button" className="icon-btn" onClick={onClick} aria-label={t.common.close}>
      <Icon name="close" />
    </button>
  );
}

interface ConfirmOptions { title: string; text?: string; confirm: string; danger?: boolean; cancel?: string }
type ConfirmFn = (options: ConfirmOptions) => Promise<boolean>;
const ConfirmCtx = createContext<ConfirmFn | null>(null);

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<(ConfirmOptions & { resolve: (ok: boolean) => void }) | null>(null);
  const { t } = useI18n();
  const ask = useCallback<ConfirmFn>((options) => new Promise<boolean>((resolve) => setState({ ...options, resolve })), []);
  const finish = (ok: boolean) => {
    state?.resolve(ok);
    setState(null);
  };
  return (
    <ConfirmCtx.Provider value={ask}>
      {children}
      <Dialog
        open={state !== null}
        onClose={() => finish(false)}
        title={state?.title ?? ''}
        footer={
          <>
            <button type="button" className="btn" onClick={() => finish(false)}>{state?.cancel ?? t.common.cancel}</button>
            <button type="button" className={state?.danger ? 'btn btn-danger' : 'btn btn-primary'} onClick={() => finish(true)} autoFocus>
              {state?.confirm}
            </button>
          </>
        }
      >
        {state?.text && <p className="muted">{state.text}</p>}
      </Dialog>
    </ConfirmCtx.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const value = useContext(ConfirmCtx);
  if (!value) throw new Error('useConfirm outside ConfirmProvider');
  return value;
}

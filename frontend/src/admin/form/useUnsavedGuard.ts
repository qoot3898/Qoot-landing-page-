import { useCallback, useEffect, useRef } from 'react';
import { useBlocker } from 'react-router-dom';
import { useI18n } from '../i18n';
import { useConfirm } from '../ui/feedback';

/**
 * Asks before leaving a screen with unsaved edits: in-app navigation and closing or reloading the tab.
 * Returns a function that lets the next navigation through (after a save that moves to a new address).
 */
export function useUnsavedGuard(dirty: boolean): () => void {
  const { t } = useI18n();
  const confirm = useConfirm();
  const skip = useRef(false);
  const blocker = useBlocker(({ currentLocation, nextLocation }) => {
    if (skip.current) {
      skip.current = false;
      return false;
    }
    return dirty && currentLocation.pathname !== nextLocation.pathname;
  });
  const latest = useRef(blocker);
  useEffect(() => {
    latest.current = blocker;
  });

  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    void confirm({ title: t.common.unsavedTitle, text: t.common.unsavedText, confirm: t.common.leave, cancel: t.common.stay, danger: true }).then((leave) => {
      const current = latest.current;
      if (current.state !== 'blocked') return;
      if (leave) current.proceed();
      else current.reset();
    });
  }, [blocker.state, confirm, t]);

  useEffect(() => {
    if (!dirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [dirty]);

  return useCallback(() => {
    skip.current = true;
  }, []);
}

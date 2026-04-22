import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { useAuthStore } from '@/stores/auth-store';

const TIMEOUT_MS  = 15 * 60 * 1_000;  // 15 minutes — NFR-005, FR-017
const WARNING_MS  = 1  * 60 * 1_000;  // warn 1 minute before expiry — UXR-504, AC-4

const ACTIVITY_EVENTS = [
  'mousemove',
  'keydown',
  'click',
  'scroll',
  'touchstart',
] as const;

/**
 * Tracks user inactivity and enforces the 15-minute session timeout (US_024, AC-4, AC-5).
 *
 * Behaviour:
 * - At 14 minutes idle: `showModal` becomes `true` → `SessionTimeoutModal` renders.
 * - At 15 minutes idle: `logout()` fires + navigate to `/login`; no toast here —
 *   the `SessionTimeoutModal` or `LoginPage` can display a contextual message.
 * - Any DOM activity (mouse, keyboard, scroll, touch) resets the idle clock via
 *   `resetActivity()` and closes the modal.
 * - Simultaneous tabs each track their own `lastActivity` independently (per-session
 *   isolation per US_024 edge case).
 *
 * Only active when `isAuthenticated = true`; cleans up all listeners on unmount or logout.
 */
export function useSessionTimeout() {
  const { isAuthenticated, lastActivity, logout, resetActivity } = useAuthStore();
  const navigate = useNavigate();
  const [showModal, setShowModal] = useState(false);
  const [countdown, setCountdown] = useState(60);
  // Ref avoids stale-closure capture of showModal inside the interval callback
  const showModalRef = useRef(showModal);
  showModalRef.current = showModal;

  useEffect(() => {
    if (!isAuthenticated) {
      setShowModal(false);
      return;
    }

    const handleActivity = () => {
      resetActivity();
      setShowModal(false);
    };

    ACTIVITY_EVENTS.forEach((evt) =>
      window.addEventListener(evt, handleActivity, { passive: true }),
    );

    const interval = setInterval(() => {
      const idle      = Date.now() - lastActivity;
      const remaining = TIMEOUT_MS - idle;

      if (remaining <= 0) {
        clearInterval(interval);
        logout();
        navigate('/login', { replace: true });
        return;
      }

      if (remaining <= WARNING_MS && !showModalRef.current) {
        setShowModal(true);
      }

      if (showModalRef.current) {
        setCountdown(Math.max(1, Math.ceil(remaining / 1_000)));
      }
    }, 1_000);

    return () => {
      ACTIVITY_EVENTS.forEach((evt) =>
        window.removeEventListener(evt, handleActivity),
      );
      clearInterval(interval);
    };
  // lastActivity changes when the user acts — re-register the interval with the updated value
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, lastActivity]);

  return { showModal, setShowModal, countdown };
}

import { Component, ErrorInfo, ReactNode } from 'react';

import { GlobalErrorPage } from './GlobalErrorPage';

interface State {
  hasError: boolean;
  error?: Error;
}

/**
 * Top-level React class error boundary (US_039 AC-4).
 *
 * Wraps the entire app component tree. Catches unhandled JavaScript errors in the render tree
 * via `componentDidCatch` and renders `GlobalErrorPage` instead of a blank white screen.
 *
 * **OWASP A09 Security Logging**: `error.message` and `info.componentStack` are logged to
 * `console.error` only — never rendered in the UI. Stack trace disclosure to end users is
 * prevented. Replace `console.error` with a monitoring service (Sentry, Datadog, etc.) in the
 * infrastructure phase.
 *
 * **"Try Again" behaviour**: `onRetry` resets `hasError` to `false`, which causes React to
 * attempt re-rendering the child tree. If the underlying error is transient (e.g. a race
 * condition on first mount), the retry will succeed.
 */
export class AppErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Phase 1: console logging only.
    // TODO (infrastructure phase): replace with monitoring service call (e.g. Sentry.captureException).
    console.error('[AppErrorBoundary] Unhandled render error:', error, info.componentStack);
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        <GlobalErrorPage
          onRetry={() => this.setState({ hasError: false })}
        />
      );
    }
    return this.props.children;
  }
}

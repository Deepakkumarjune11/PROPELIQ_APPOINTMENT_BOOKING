import '@fontsource/roboto/300.css';
import '@fontsource/roboto/400.css';
import '@fontsource/roboto/500.css';
import '@fontsource/roboto/700.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import App from './App';

const rootElement = document.getElementById('root');

// Fail fast with a clear message rather than a cryptic null-reference downstream
if (!rootElement) {
  throw new Error(
    'Root element with id "root" not found. Verify that index.html contains <div id="root"></div>.',
  );
}

// Development-only: @axe-core/react reports WCAG violations to the browser console.
// Dynamic import + import.meta.env.DEV guard ensures zero production bundle impact
// (Vite tree-shakes this branch entirely in production builds).
if (import.meta.env.DEV) {
  const React = await import('react');
  const ReactDOM = await import('react-dom');
  const axe = await import('@axe-core/react');
  axe.default(React.default, ReactDOM.default, 1000);
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

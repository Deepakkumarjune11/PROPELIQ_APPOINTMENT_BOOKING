import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { Navigate, RouterProvider, createBrowserRouter } from 'react-router-dom';

import AuthenticatedLayout from '@/components/layout/AuthenticatedLayout';
import LoginPage from '@/pages/LoginPage';
import { healthcareTheme } from '@/theme/healthcare-theme';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Cache server data for 5 minutes before marking stale
      staleTime: 5 * 60 * 1000,
      retry: 1,
      // Avoid re-fetching on window focus in clinical workflows where data freshness is managed deliberately
      refetchOnWindowFocus: false,
    },
  },
});

const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <AuthenticatedLayout />,
    children: [
      // Nested authenticated routes will be added in subsequent tasks
    ],
  },
  {
    // Catch-all sends unknown paths to login
    path: '*',
    element: <Navigate to="/login" replace />,
  },
]);

export default function App() {
  return (
    <ThemeProvider theme={healthcareTheme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  );
}

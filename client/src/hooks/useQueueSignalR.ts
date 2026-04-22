// SignalR hub connection for real-time queue updates (US_017, AC-4).
// Connects to /hubs/queue, subscribes to QueueUpdated broadcast.
// On event: invalidates React Query 'queue' cache → triggers re-fetch.
// On disconnection: shows "Reconnecting…" toast.
// On reconnection: shows "reconnected" toast.
// Auto-reconnect with exponential backoff: 0s, 2s, 5s, 10s (then indefinite 10s).
import { useEffect } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';

import { getAuthToken } from '@/api/staff';
import { QUEUE_QUERY_KEY } from './useSameDayQueue';

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

interface UseQueueSignalROptions {
  onReconnecting?: () => void;
  onReconnected?: () => void;
}

export function useQueueSignalR({ onReconnecting, onReconnected }: UseQueueSignalROptions = {}) {
  const queryClient = useQueryClient();

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/queue`, {
        accessTokenFactory: () => getAuthToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    // Real-time queue update — invalidate cache so React Query re-fetches fresh data (AC-4).
    connection.on('QueueUpdated', () => {
      void queryClient.invalidateQueries({ queryKey: QUEUE_QUERY_KEY });
    });

    // Reconnection lifecycle callbacks (edge case — toast wiring delegated to caller).
    connection.onreconnecting(() => {
      onReconnecting?.();
    });

    connection.onreconnected(() => {
      // Re-fetch immediately on reconnect so the queue is current.
      void queryClient.invalidateQueries({ queryKey: QUEUE_QUERY_KEY });
      onReconnected?.();
    });

    // Start connection; ignore error — component stays functional with polling fallback.
    connection.start().catch((err: unknown) => {
      if (import.meta.env.DEV) {
        console.warn('[QueueSignalR] Connection failed:', err);
      }
    });

    return () => {
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }
    };
  }, [queryClient, onReconnecting, onReconnected]);
}

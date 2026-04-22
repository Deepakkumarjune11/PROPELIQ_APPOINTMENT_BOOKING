import { useQuery } from '@tanstack/react-query';

import api from '@/lib/api';
import type { AdminUserDto } from '@/api/adminUsers';

export function useAdminUsers() {
  return useQuery<AdminUserDto[]>({
    queryKey: ['adminUsers'],
    queryFn: () => api.get<AdminUserDto[]>('/api/v1/admin/users').then((r) => r.data),
    staleTime: 30_000,
  });
}

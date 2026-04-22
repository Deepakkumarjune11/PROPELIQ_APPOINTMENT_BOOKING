import { useMutation, useQueryClient } from '@tanstack/react-query';

import api from '@/lib/api';
import type { AdminUserDto, CreateUserRequest } from '@/api/adminUsers';

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation<AdminUserDto, Error, CreateUserRequest>({
    mutationFn: (data) =>
      api.post<AdminUserDto>('/api/v1/admin/users', data).then((r) => r.data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['adminUsers'] });
    },
  });
}

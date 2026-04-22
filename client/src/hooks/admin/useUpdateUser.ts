import { useMutation, useQueryClient } from '@tanstack/react-query';

import api from '@/lib/api';
import type { AdminUserDto, UpdateUserRequest } from '@/api/adminUsers';

export function useUpdateUser(userId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation<AdminUserDto, Error, UpdateUserRequest>({
    mutationFn: (data) =>
      api.put<AdminUserDto>(`/api/v1/admin/users/${userId}`, data).then((r) => r.data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['adminUsers'] });
    },
  });
}

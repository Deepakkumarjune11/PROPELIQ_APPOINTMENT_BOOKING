import { useMutation } from '@tanstack/react-query';
import api from '@/lib/api';

export function useResetPassword(userId: string) {
  return useMutation<void, Error, { newPassword: string }>({
    mutationFn: (data) =>
      api.patch(`/api/v1/admin/users/${userId}/reset-password`, data).then(() => undefined),
  });
}

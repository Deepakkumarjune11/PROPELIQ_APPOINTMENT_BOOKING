import { useMutation, useQueryClient } from '@tanstack/react-query';

import api from '@/lib/api';

export function useToggleUserStatus(userId: string, currentlyActive: boolean) {
  const queryClient = useQueryClient();
  const action = currentlyActive ? 'disable' : 'enable';
  return useMutation<void, Error, void>({
    mutationFn: () =>
      api.patch(`/api/v1/admin/users/${userId}/${action}`).then(() => undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['adminUsers'] });
    },
  });
}

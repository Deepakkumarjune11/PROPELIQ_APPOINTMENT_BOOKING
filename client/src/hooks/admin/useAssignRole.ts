import { useMutation, useQueryClient } from '@tanstack/react-query';

import api from '@/lib/api';
import type { AssignRoleRequest } from '@/api/adminUsers';

export function useAssignRole(userId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, Error, AssignRoleRequest>({
    mutationFn: (data) =>
      api.patch(`/api/v1/admin/users/${userId}/role`, data).then(() => undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['adminUsers'] });
    },
  });
}

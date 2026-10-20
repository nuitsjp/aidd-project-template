import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Principal } from '../../../../contracts/notes.ts';
export interface Session {
  user: Principal | null;
  mode: 'demo' | 'proxy';
}
async function request<T>(url: string, body?: unknown): Promise<T> {
  const response = await fetch(
    url,
    body === undefined
      ? {}
      : {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        },
  );
  if (!response.ok) throw new Error('利用者情報を取得できませんでした。');
  return response.json() as Promise<T>;
}
export const useSession = () =>
  useQuery({
    queryKey: ['session'],
    queryFn: () => request<Session>('/api/session'),
    staleTime: Infinity,
  });
export function useSignIn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (user: string) =>
      request<{
        user: Principal;
      }>('/api/demo/sign-in', { user }),
    onSuccess: async ({ user }) => {
      await queryClient.cancelQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(['session'], { user, mode: 'demo' });
    },
  });
}
export function useSignOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => request('/api/demo/sign-out', {}),
    onSuccess: async () => {
      await queryClient.cancelQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(['session'], { user: null, mode: 'demo' });
    },
  });
}

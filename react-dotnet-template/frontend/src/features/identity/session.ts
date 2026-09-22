import { useMutation, useQuery } from '@tanstack/react-query';
import type { Principal } from '../../../../contracts/notes.ts';
import { postJson, requestJson } from '../client.ts';
import { queryClient } from '../query-client.ts';
export interface Session {
    user: Principal | null;
    mode: 'demo' | 'proxy';
}
export const useSession = () => useQuery({ queryKey: ['session'], queryFn: () => requestJson<Session>('/api/session'), staleTime: Infinity });
export const useSignIn = () => useMutation({ mutationFn: (user: string) => postJson<{
        user: Principal;
    }>('/api/demo/sign-in', { user }),
    onSuccess: async ({ user }) => { await queryClient.cancelQueries({ queryKey: ['notes'] }); queryClient.removeQueries({ queryKey: ['notes'] }); queryClient.setQueryData(['session'], { user, mode: 'demo' }); } });
export const useSignOut = () => useMutation({ mutationFn: () => postJson<{ ok: true }>('/api/demo/sign-out', {}),
    onSuccess: async () => { await queryClient.cancelQueries({ queryKey: ['notes'] }); queryClient.removeQueries({ queryKey: ['notes'] }); queryClient.setQueryData(['session'], { user: null, mode: 'demo' }); } });

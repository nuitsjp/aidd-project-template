import { useMutation, useQuery } from '@tanstack/react-query';
import type {
  Session,
  SignInInput,
  SignInOutput,
  SuccessOutput,
} from '../../../../contracts/notes.ts';
import { postJson, requestJson } from '../client.ts';
import { queryClient } from '../query-client.ts';
export type { Session };
export const useSession = () =>
  useQuery({
    queryKey: ['session'],
    queryFn: () => requestJson<Session>('/api/session'),
    staleTime: Infinity,
  });
export const useSignIn = () =>
  useMutation({
    mutationFn: (user: string) =>
      postJson<SignInOutput>('/api/demo/sign-in', { user } satisfies SignInInput),
    onSuccess: async ({ user }) => {
      await queryClient.cancelQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(['session'], { user, mode: 'demo' });
    },
  });
export const useSignOut = () =>
  useMutation({
    mutationFn: () => postJson<SuccessOutput>('/api/demo/sign-out', {}),
    onSuccess: async () => {
      await queryClient.cancelQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(['session'], { user: null, mode: 'demo' });
    },
  });

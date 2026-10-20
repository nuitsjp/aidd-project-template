import type { ReactNode } from 'react';
import { beforeEach, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useSignIn } from '../../../../frontend/src/features/identity/session.ts';
import { notesOptions } from '../../../../frontend/src/features/notes/queries.ts';
import { TestProviders, createTestQueryClient } from '../../render.tsx';

vi.mock('@notes-access', () => ({
  listNotes: vi.fn(),
  saveNote: vi.fn(),
  removeNote: vi.fn(),
  previewMany: vi.fn(),
  importMany: vi.fn(),
  watchNotes: vi.fn(),
}));

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

// 同じブラウザー内での利用者切替は E2E で扱わない（E2E は利用者ごとに別の Context）ため、部品で確認する。
it('利用者を切り替えると前の利用者のメモをキャッシュから破棄する', async () => {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(notesOptions().queryKey, [
    {
      id: '00000000-0000-4000-8000-000000000001',
      title: 'Aliceのメモ',
      body: '',
      version: 1,
      updatedAt: '2026-01-01T00:00:00Z',
    },
  ]);
  fetchMock.mockResolvedValue(
    new Response(JSON.stringify({ user: { id: 'bob', name: 'Bob' } }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  );
  const wrapper = ({ children }: { children: ReactNode }) => (
    <TestProviders queryClient={queryClient}>{children}</TestProviders>
  );
  const { result } = renderHook(() => useSignIn(), { wrapper });

  await act(() => result.current.mutateAsync('bob'));

  expect(queryClient.getQueryData(notesOptions().queryKey)).toBeUndefined();
  expect(queryClient.getQueryData(['session'])).toEqual({
    user: { id: 'bob', name: 'Bob' },
    mode: 'demo',
  });
});

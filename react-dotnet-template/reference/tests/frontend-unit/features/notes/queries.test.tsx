import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { listNotes, watchNotes } from '@notes-access';
import { useNotes, useNotesSubscription } from '../../../../frontend/src/features/notes/queries.ts';
import { TestProviders, createTestQueryClient } from '../../render.tsx';

vi.mock('@notes-access', () => ({
  listNotes: vi.fn(),
  saveNote: vi.fn(),
  removeNote: vi.fn(),
  previewMany: vi.fn(),
  importMany: vi.fn(),
  watchNotes: vi.fn(),
}));

type Subscription = { onChange: () => void; onStatus: (ready: boolean) => void; close: () => void };
let subscriptions: Subscription[] = [];

beforeEach(() => {
  subscriptions = [];
  vi.mocked(listNotes).mockReset().mockResolvedValue([]);
  vi.mocked(watchNotes)
    .mockReset()
    .mockImplementation((onChange, onStatus) => {
      const close = vi.fn();
      subscriptions.push({ onChange, onStatus, close });
      return close;
    });
});

function renderSubscription(ownerId: string) {
  const queryClient = createTestQueryClient();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <TestProviders queryClient={queryClient}>{children}</TestProviders>
  );
  return renderHook(
    ({ owner }: { owner: string }) => ({ notes: useNotes(), ready: useNotesSubscription(owner) }),
    { wrapper, initialProps: { owner: ownerId } },
  );
}

// 変更通知の接続・切断は E2E で再現しにくい障害時動作と非同期の競合のため、部品で確認する。
describe('useNotesSubscription', () => {
  it('接続状態を返し、変更通知を受けると一覧を再取得する', async () => {
    // Arrange
    const { result } = renderSubscription('alice');
    await waitFor(() => expect(listNotes).toHaveBeenCalledTimes(1));

    // Act
    act(() => subscriptions[0]!.onStatus(true));
    act(() => subscriptions[0]!.onChange());

    // Assert
    expect(result.current.ready).toBe(true);
    await waitFor(() => expect(listNotes).toHaveBeenCalledTimes(2));
  });

  it('切断されると未接続を返す', () => {
    // Arrange
    const { result } = renderSubscription('alice');
    act(() => subscriptions[0]!.onStatus(true));

    // Act
    act(() => subscriptions[0]!.onStatus(false));

    // Assert
    expect(result.current.ready).toBe(false);
  });

  it('利用者が替わると前の購読を閉じ、新しい利用者で購読し直す', () => {
    // Arrange
    const { result, rerender } = renderSubscription('alice');
    act(() => subscriptions[0]!.onStatus(true));

    // Act
    rerender({ owner: 'bob' });

    // Assert
    expect(subscriptions[0]!.close).toHaveBeenCalledTimes(1);
    expect(subscriptions).toHaveLength(2);
    expect(result.current.ready).toBe(false);
  });
});

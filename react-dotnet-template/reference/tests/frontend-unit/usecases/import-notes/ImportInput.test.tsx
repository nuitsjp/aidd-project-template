import { beforeEach, expect, it, vi } from 'vitest';
import { act, fireEvent, screen } from '@testing-library/react';
import type { BulkPreview } from '@contracts/notes.ts';
import { previewMany } from '@notes-access';
import { ImportDialogue } from '../../../../frontend/src/usecases/import-notes/ImportDialogue.tsx';
import { ImportInput } from '../../../../frontend/src/usecases/import-notes/ImportInput.tsx';
import { pageRoute, renderWithRouter } from '../../render.tsx';

vi.mock('@notes-access', () => ({
  listNotes: vi.fn(),
  saveNote: vi.fn(),
  removeNote: vi.fn(),
  previewMany: vi.fn(),
  importMany: vi.fn(),
  watchNotes: vi.fn(() => () => {}),
}));

beforeEach(() => {
  vi.mocked(previewMany).mockReset();
  vi.spyOn(window, 'confirm').mockReturnValue(true);
});

// 確認の応答待ちと画面移動の競合は E2E で再現しにくい非同期の競合のため、部品で確認する。
it('確認の応答前に画面を離れた場合、応答後に確認画面へ移動しない', async () => {
  // Arrange
  let respond: (preview: BulkPreview) => void = () => {};
  vi.mocked(previewMany).mockReturnValue(
    new Promise((resolve) => {
      respond = resolve;
    }),
  );
  const { router } = renderWithRouter((root) => {
    const dialogue = pageRoute(() => root, '/import', ImportDialogue);
    return [
      dialogue.addChildren([
        pageRoute(() => dialogue, '/', ImportInput),
        pageRoute(
          () => dialogue,
          '/confirm',
          () => <p>確認画面</p>,
        ),
      ]),
      pageRoute(
        () => root,
        '/notes',
        () => <p>メモ一覧</p>,
      ),
    ];
  }, '/import');
  fireEvent.change(await screen.findByLabelText('タイトル一覧'), { target: { value: '一件目' } });
  fireEvent.click(screen.getByRole('button', { name: '内容を確認する' }));

  // Act
  await act(() => router.navigate({ to: '/notes' }));
  await act(async () => respond({ titles: ['一件目'], body: '' }));

  // Assert
  expect(router.state.location.pathname).toBe('/notes');
  expect(screen.getByText('メモ一覧')).toBeInTheDocument();
});

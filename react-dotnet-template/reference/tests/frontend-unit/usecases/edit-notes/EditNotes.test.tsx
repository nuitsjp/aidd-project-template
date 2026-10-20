import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, screen } from '@testing-library/react';
import type { Note } from '@contracts/notes.ts';
import { listNotes, saveNote } from '@notes-access';
import { EditNotes } from '../../../../frontend/src/usecases/edit-notes/EditNotes.tsx';
import { pageRoute, renderWithRouter } from '../../render.tsx';

vi.mock('@notes-access', () => ({
  listNotes: vi.fn(),
  saveNote: vi.fn(),
  removeNote: vi.fn(),
  previewMany: vi.fn(),
  importMany: vi.fn(),
  watchNotes: vi.fn(() => () => {}),
}));

const saved: Note = {
  id: '00000000-0000-4000-8000-000000000001',
  title: '保存したメモ',
  body: '本文',
  version: 1,
  updatedAt: '2026-01-01T00:00:00Z',
};

beforeEach(() => {
  vi.mocked(listNotes).mockReset();
  vi.mocked(saveNote).mockReset();
});

// 確定後の再取得失敗は E2E で再現しにくい障害時動作のため、部品で確認する。
it('保存後の一覧の再取得に失敗しても保存結果を表示し、一覧の取得失敗を別に示す', async () => {
  // Arrange
  vi.mocked(listNotes).mockResolvedValueOnce([]).mockRejectedValueOnce(new Error('network'));
  vi.mocked(saveNote).mockResolvedValue(saved);
  renderWithRouter((root) => [pageRoute(() => root, '/notes', EditNotes)], '/notes');
  await screen.findByText('メモはまだありません。');

  // Act
  fireEvent.change(screen.getByLabelText('タイトル'), { target: { value: saved.title } });
  fireEvent.change(screen.getByLabelText('本文'), { target: { value: saved.body } });
  fireEvent.click(screen.getByRole('button', { name: '保存する' }));

  // Assert
  expect(await screen.findByRole('status')).toHaveTextContent('保存しました');
  expect(await screen.findByText('一覧の取得に失敗しました')).toBeInTheDocument();
  expect(screen.getByLabelText('タイトル')).toHaveValue(saved.title);
  expect(listNotes).toHaveBeenCalledTimes(2);
});

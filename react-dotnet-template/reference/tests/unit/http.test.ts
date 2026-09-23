import { beforeEach, describe, expect, it, vi } from 'vitest';
import { importMany, listNotes, previewMany, removeNote, saveNote } from '../../frontend/src/features/notes/access.ts';
import { requestJson } from '../../frontend/src/features/client.ts';
import { readErrorMessage } from '../../frontend/src/shared/errors.ts';

const fetchMock = vi.fn<typeof fetch>();

function response(body: unknown, status = 200): Response {
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('メモHTTPアクセス', () => {
    beforeEach(() => {
        fetchMock.mockReset();
        vi.stubGlobal('fetch', fetchMock);
    });

    it('一覧はGET /api/notesを呼び出す', async () => {
        const notes = [{ id: 'note-1', title: '題名', body: '本文', version: 1, updatedAt: '2026-01-01T00:00:00Z' }];
        fetchMock.mockResolvedValue(response(notes));
        const signal = new AbortController().signal;
        await expect(listNotes(signal)).resolves.toEqual(notes);
        expect(fetchMock).toHaveBeenCalledWith('/api/notes', { signal });
    });

    it('保存・削除・プレビュー・一括登録をJSON POSTへ対応付ける', async () => {
        const save = { id: 'note-1', version: 1, title: '題名', body: '本文' };
        const note = { id: 'note-1', title: '題名', body: '本文', version: 2, updatedAt: '2026-01-01T00:00:00Z' };
        fetchMock
            .mockResolvedValueOnce(response(note))
            .mockResolvedValueOnce(response({ ok: true }))
            .mockResolvedValueOnce(response({ titles: ['一件目'], body: '共通本文' }))
            .mockResolvedValueOnce(response({ count: 1 }));
        await saveNote(save);
        await removeNote({ id: 'note-1', version: 2 });
        await previewMany({ titles: '一件目', body: '共通本文' });
        await importMany({ titles: '一件目', body: '共通本文' });
        expect(fetchMock.mock.calls).toEqual([
            ['/api/notes/save', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(save) }],
            ['/api/notes/remove', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: 'note-1', version: 2 }) }],
            ['/api/notes/preview', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ titles: '一件目', body: '共通本文' }) }],
            ['/api/notes/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ titles: '一件目', body: '共通本文' }) }],
        ]);
    });

    it('HTTPエラーの公開メッセージを保持する', async () => {
        fetchMock.mockResolvedValue(response({ title: '更新が競合しました。', status: 409, detail: '版が古くなっています。' }, 409));
        await expect(saveNote({ title: '題名', body: '本文', id: 'note-1', version: 1 })).rejects.toSatisfy(error => {
            expect(readErrorMessage(error)).toBe('版が古くなっています。');
            return true;
        });
    });

    it('成功応答の不正JSONを成功値として扱わない', async () => {
        fetchMock.mockResolvedValue(new Response('not-json', { status: 200 }));
        await expect(requestJson('/api/notes')).rejects.toThrow('サーバーの応答を読み取れませんでした。');
    });
});

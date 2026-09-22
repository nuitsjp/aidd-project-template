import { postJson, requestJson } from '../client.ts';
import type { BulkInput, BulkPreview, BulkResult, Note, SaveNote } from '../../../../contracts/notes.ts';
export const listNotes = (signal?: AbortSignal) => requestJson<Note[]>('/api/notes', { signal });
export const saveNote = (input: SaveNote) => postJson<Note>('/api/notes/save', input);
export const removeNote = (input: {
    id: string;
    version: number;
}) => postJson<{ ok: true }>('/api/notes/remove', input);
export const previewMany = (input: BulkInput) => postJson<BulkPreview>('/api/notes/preview', input);
export const importMany = (input: BulkInput) => postJson<BulkResult>('/api/notes/import', input);
export function watchNotes(onChange: () => void, onStatus: (ready: boolean) => void): () => void {
    const source = new EventSource('/events/notes');
    const ready = () => { onStatus(true); onChange(); }; // 再接続時は履歴再生でなく現在値へ追従する。
    source.addEventListener('ready', ready);
    source.addEventListener('notes.changed', onChange);
    source.onerror = () => onStatus(false);
    return () => { source.removeEventListener('ready', ready); source.removeEventListener('notes.changed', onChange); source.close(); };
}

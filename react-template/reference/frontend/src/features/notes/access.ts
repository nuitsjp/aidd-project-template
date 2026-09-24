import { rpc } from '../client.ts';
import type { BulkInput, SaveNote } from '../../../../contracts/notes.ts';
export const listNotes = (signal?: AbortSignal) => rpc.notes.list.query(undefined, { signal });
export const saveNote = (input: SaveNote) => rpc.notes.save.mutate(input);
export const removeNote = (input: {
    id: string;
    version: number;
}) => rpc.notes.remove.mutate(input);
export const previewMany = (input: BulkInput) => rpc.notes.preview.mutate(input);
export const importMany = (input: BulkInput) => rpc.notes.importMany.mutate(input);
export function watchNotes(onChange: () => void, onStatus: (ready: boolean) => void): () => void {
    const source = new EventSource('/events/notes');
    const ready = () => { onStatus(true); onChange(); }; // 再接続時は履歴再生でなく現在値へ追従する。
    source.addEventListener('ready', ready);
    source.addEventListener('notes.changed', onChange);
    source.onerror = () => onStatus(false);
    return () => { source.removeEventListener('ready', ready); source.removeEventListener('notes.changed', onChange); source.close(); };
}

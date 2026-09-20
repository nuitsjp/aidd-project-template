import { useEffect, useState } from 'react';
import { queryOptions, useMutation, useQuery } from '@tanstack/react-query';
import { listNotes, saveNote, removeNote, previewMany, importMany, watchNotes } from '@notes-access';
import { queryClient } from '../query-client.ts';
export const notesOptions = () => queryOptions({ queryKey: ['notes'] as const, queryFn: ({ signal }) => listNotes(signal) });
export const refreshNotes = () => queryClient.invalidateQueries({ queryKey: ['notes'] });
export const useNotes = () => useQuery(notesOptions());
// 再取得失敗はQuery側で表示する。保存済みの操作を失敗に変更しない。
const refreshAfterCommit = () => { void refreshNotes().catch(() => { }); };
export const useSaveNote = () => useMutation({ mutationFn: saveNote, onSuccess: refreshAfterCommit });
export const useRemoveNote = () => useMutation({ mutationFn: removeNote, onSuccess: refreshAfterCommit });
export const usePreviewMany = () => useMutation({ mutationFn: previewMany });
export const useImportMany = () => useMutation({ mutationFn: importMany, onSuccess: refreshAfterCommit });
export function useNotesSubscription(ownerId: string | undefined): boolean {
    const [ready, setReady] = useState(false);
    useEffect(() => {
        setReady(false);
        if (!ownerId)
            return;
        return watchNotes(() => { void refreshNotes().catch(() => { }); }, setReady);
    }, [ownerId]);
    return ready;
}

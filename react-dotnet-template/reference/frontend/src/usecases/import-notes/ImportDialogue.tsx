import { createContext, useContext, useState } from 'react';
import { Outlet } from '@tanstack/react-router';
import { Text, Title } from '@mantine/core';
import type { BulkInput, BulkPreview } from '../../../../contracts/notes.ts';
import { useDraftDirty } from '../../shared/DraftContext.tsx';
interface Value {
    input: BulkInput;
    setInput: (value: BulkInput) => void;
    preview: BulkPreview | null;
    setPreview: (value: BulkPreview | null) => void;
    clear: () => void;
}
const Context = createContext<Value | null>(null);
export function ImportDialogue() {
    const [input, setInput] = useState<BulkInput>({ titles: '', body: '' });
    const [preview, setPreview] = useState<BulkPreview | null>(null);
    useDraftDirty(input.titles !== '' || input.body !== '');
    function updateInput(value: BulkInput) {
        setInput(value);
        setPreview(null);
    }
    return <Context.Provider value={{ input, setInput: updateInput, preview, setPreview, clear: () => { setInput({ titles: '', body: '' }); setPreview(null); } }}>
    <div className="wizard">
    <div className="section-label">USE CASE 02</div>
    <Title order={1} className="page-heading">メモを一括登録する</Title>
    <Text c="dimmed" mb="xl">入力 → 確認 → まとめて保存。途中失敗は全件をロールバックします。</Text>
    <Outlet />
    </div>
    </Context.Provider>;
}
export function useImportDialogue() {
    const value = useContext(Context);
    if (!value)
        throw new Error('ImportDialogueが必要です');
    return value;
}

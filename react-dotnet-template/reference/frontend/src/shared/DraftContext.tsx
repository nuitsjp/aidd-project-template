import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useBlocker } from '@tanstack/react-router';
const Context = createContext<{
    dirty: boolean;
    setDirty: (value: boolean) => void;
} | null>(null);
export function DraftProvider({ children }: {
    children: ReactNode;
}) {
    const [dirty, setDirty] = useState(false);
    useBlocker({ shouldBlockFn: ({ current, next }) => {
            if (current.pathname.startsWith('/import') && next.pathname.startsWith('/import'))
                return false;
            return dirty && !window.confirm('未保存の入力を破棄して移動しますか？');
        }, enableBeforeUnload: dirty });
    return <Context.Provider value={{ dirty, setDirty }}>{children}</Context.Provider>;
}
export function useDraft() {
    const value = useContext(Context);
    if (!value)
        throw new Error('DraftProviderが必要です');
    return value;
}
export function useDraftDirty(dirty: boolean) { const { setDirty } = useDraft(); useEffect(() => { setDirty(dirty); return () => setDirty(false); }, [dirty, setDirty]); }

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useBlocker } from '@tanstack/react-router';
const Context = createContext<{
  dirty: boolean;
  setDirty: (value: boolean) => void;
} | null>(null);
export function DraftProvider({ children }: { children: ReactNode }) {
  const [dirty, setDirty] = useState(false);
  return <Context.Provider value={{ dirty, setDirty }}>{children}</Context.Provider>;
}
export function useDraft() {
  const value = useContext(Context);
  if (!value) throw new Error('DraftProviderが必要です');
  return value;
}
// 未保存の下書きを持つ画面が呼ぶ。keepsDraftは下書きを引き継ぐ遷移先を判定する。
export function useDraftDirty(
  dirty: boolean,
  keepsDraft: (pathname: string) => boolean = () => false,
) {
  const { setDirty } = useDraft();
  useEffect(() => {
    setDirty(dirty);
    return () => setDirty(false);
  }, [dirty, setDirty]);
  useBlocker({
    shouldBlockFn: ({ next }) =>
      dirty &&
      !keepsDraft(next.pathname) &&
      !window.confirm('未保存の入力を破棄して移動しますか？'),
    enableBeforeUnload: dirty,
  });
}

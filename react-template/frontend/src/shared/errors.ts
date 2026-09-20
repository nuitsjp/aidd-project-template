import type { PublicFault } from '../../../contracts/notes.ts';
export function readFault(error: unknown): PublicFault {
    if (error && typeof error === 'object' && 'data' in error) {
        const data = error.data;
        if (data && typeof data === 'object' && 'appError' in data) {
            const value = data.appError;
            if (value && typeof value === 'object' && 'code' in value && 'message' in value && typeof value.code === 'string' && typeof value.message === 'string') {
                const known = ['VALIDATION', 'TITLE_EXISTS', 'NOT_FOUND', 'EDIT_CONFLICT', 'UNAUTHENTICATED', 'INTERNAL'];
                if (known.includes(value.code))
                    return value as PublicFault;
            }
        }
    }
    return { code: 'INTERNAL', message: '処理を完了できませんでした。接続とサーバーの状態を確認してください。' };
}

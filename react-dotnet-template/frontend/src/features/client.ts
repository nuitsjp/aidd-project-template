import type { FaultCode, PublicFault } from '../../../contracts/notes.ts';

const faultCodes: readonly FaultCode[] = ['VALIDATION', 'TITLE_EXISTS', 'NOT_FOUND', 'EDIT_CONFLICT', 'UNAUTHENTICATED', 'INTERNAL'];
const fallbackFault: PublicFault = { code: 'INTERNAL', message: '処理を完了できませんでした。接続とサーバーの状態を確認してください。' };

export interface HttpErrorData {
    appError: PublicFault;
}

export class HttpError extends Error {
    readonly data: HttpErrorData;
    readonly status: number;

    constructor(status: number, fault: PublicFault) {
        super(fault.message);
        this.name = 'HttpError';
        this.status = status;
        this.data = { appError: fault };
    }
}

function readPublicFault(value: unknown): PublicFault {
    if (!value || typeof value !== 'object' || !('appError' in value))
        return fallbackFault;
    const candidate = value.appError;
    if (!candidate || typeof candidate !== 'object' || !('code' in candidate) || !('message' in candidate))
        return fallbackFault;
    const code = candidate.code;
    const message = candidate.message;
    if (typeof code !== 'string' || !faultCodes.includes(code as FaultCode) || typeof message !== 'string')
        return fallbackFault;
    const fieldErrors = 'fieldErrors' in candidate && candidate.fieldErrors && typeof candidate.fieldErrors === 'object'
        ? Object.fromEntries(Object.entries(candidate.fieldErrors).filter(([, item]) => typeof item === 'string'))
        : undefined;
    return fieldErrors && Object.keys(fieldErrors).length > 0
        ? { code: code as FaultCode, message, fieldErrors }
        : { code: code as FaultCode, message };
}

export async function requestJson<T>(url: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch(url, init);
    let body: unknown;
    try {
        body = await response.json();
    }
    catch {
        if (response.ok)
            throw new Error('サーバーの応答を読み取れませんでした。');
        body = undefined;
    }
    if (!response.ok)
        throw new HttpError(response.status, readPublicFault(body));
    return body as T;
}

export function postJson<T>(url: string, body: unknown): Promise<T> {
    return requestJson<T>(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
}

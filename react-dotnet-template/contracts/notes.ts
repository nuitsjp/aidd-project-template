// 公開契約のみ。Node.js・DB・Reactへ依存しない。
export interface Principal {
    id: string;
    name: string;
}
export interface Note {
    id: string;
    title: string;
    body: string;
    version: number;
    updatedAt: string;
}
export interface SaveNote {
    id?: string;
    version?: number;
    title: string;
    body: string;
}
export interface BulkInput {
    titles: string;
    body: string;
}
export interface BulkPreview {
    titles: string[];
    body: string;
}
export interface BulkResult {
    count: number;
}
export type FaultCode = 'VALIDATION' | 'TITLE_EXISTS' | 'NOT_FOUND' | 'EDIT_CONFLICT' | 'UNAUTHENTICATED' | 'INTERNAL';
export interface PublicFault {
    code: FaultCode;
    message: string;
    fieldErrors?: Record<string, string>;
}

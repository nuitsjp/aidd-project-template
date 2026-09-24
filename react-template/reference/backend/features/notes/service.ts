import { randomUUID } from 'node:crypto';
import { EventEmitter } from 'node:events';
import type { DatabaseSync } from 'node:sqlite';
import type { BulkInput, BulkPreview, BulkResult, Note, SaveNote } from '../../../contracts/notes.ts';
import { Fault } from '../../fault.ts';
export class NotesService {
    private readonly db: DatabaseSync;
    private readonly events = new EventEmitter();
    private readonly reportNotificationError: (error: unknown) => void;
    constructor(db: DatabaseSync, reportNotificationError: (error: unknown) => void) { this.db = db; this.reportNotificationError = reportNotificationError; }
    list(ownerId: string): Note[] {
        return this.db.prepare('SELECT id,title,body,version,updated_at AS updatedAt FROM notes WHERE owner_id=? ORDER BY updated_at DESC,id').all(ownerId) as unknown as Note[];
    }
    get(ownerId: string, id: string): Note {
        const note = this.db.prepare('SELECT id,title,body,version,updated_at AS updatedAt FROM notes WHERE owner_id=? AND id=?').get(ownerId, id);
        if (!note)
            throw new Fault('NOT_FOUND', '対象のメモが見つかりません。');
        return note as unknown as Note;
    }
    save(ownerId: string, input: SaveNote): Note {
        const title = validateTitle(input.title);
        validateBody(input.body);
        if ((input.id === undefined) !== (input.version === undefined))
            throw new Fault('VALIDATION', '編集対象と版を指定してください。');
        const id = input.id ?? randomUUID();
        try {
            this.db.exec('BEGIN IMMEDIATE');
            if (input.id) {
                const current = this.get(ownerId, input.id);
                if (current.version !== input.version)
                    throw new Fault('EDIT_CONFLICT', '別の操作で更新されています。下書きを保持したまま、最新版を確認してください。');
                this.db.prepare('UPDATE notes SET title=?,body=?,version=version+1,updated_at=? WHERE owner_id=? AND id=?').run(title, input.body, new Date().toISOString(), ownerId, id);
            }
            else {
                this.db.prepare('INSERT INTO notes(id,owner_id,title,body,version,updated_at) VALUES(?,?,?,?,1,?)').run(id, ownerId, title, input.body, new Date().toISOString());
            }
            const saved = this.get(ownerId, id);
            this.db.exec('COMMIT');
            this.notify(ownerId);
            return saved;
        }
        catch (error) {
            if (this.db.isTransaction)
                this.db.exec('ROLLBACK');
            throw translateDatabaseError(error);
        }
    }
    remove(ownerId: string, id: string, version: number): void {
        this.db.exec('BEGIN IMMEDIATE');
        try {
            const current = this.get(ownerId, id);
            if (current.version !== version)
                throw new Fault('EDIT_CONFLICT', '対象が更新されています。最新版を確認してください。');
            this.db.prepare('DELETE FROM notes WHERE owner_id=? AND id=?').run(ownerId, id);
            this.db.exec('COMMIT');
        }
        catch (error) {
            this.db.exec('ROLLBACK');
            throw error;
        }
        this.notify(ownerId);
    }
    preview(input: BulkInput): BulkPreview {
        validateBody(input.body);
        const raw = input.titles.split(/\r?\n/).filter((line) => line.trim().length > 0);
        if (raw.length < 1 || raw.length > 100)
            throw new Fault('VALIDATION', 'タイトルは1〜100件で入力してください。', { titles: '1〜100行で入力してください。' });
        const titles = raw.map(validateTitle);
        if (new Set(titles).size !== titles.length)
            throw new Fault('VALIDATION', '入力内でタイトルが重複しています。', { titles: '重複を取り除いてください。' });
        return { titles, body: input.body };
    }
    importMany(ownerId: string, input: BulkInput): BulkResult {
        const preview = this.preview(input); // プレビューが改変されても保存時に成立条件を検証する。
        this.db.exec('BEGIN IMMEDIATE');
        try {
            const stmt = this.db.prepare('INSERT INTO notes(id,owner_id,title,body,version,updated_at) VALUES(?,?,?,?,1,?)');
            for (const title of preview.titles)
                stmt.run(randomUUID(), ownerId, title, preview.body, new Date().toISOString());
            this.db.exec('COMMIT');
        }
        catch (error) {
            this.db.exec('ROLLBACK');
            throw translateDatabaseError(error);
        }
        this.notify(ownerId);
        return { count: preview.titles.length };
    }
    subscribe(ownerId: string, listener: () => void): () => void {
        this.events.on(ownerId, listener);
        return () => { this.events.off(ownerId, listener); };
    }
    private notify(ownerId: string): void {
        // 通知失敗で確定済みの保存を失敗扱いにしない。購読者の実装では例外を外へ出さない。
        try {
            this.events.emit(ownerId);
        }
        catch (error) {
            this.reportNotificationError(error);
        }
    }
}
function validateTitle(value: string): string {
    const title = value.trim();
    if (title.length === 0 || [...title].length > 100)
        throw new Fault('VALIDATION', 'タイトルは1〜100文字で入力してください。', { title: '1〜100文字で入力してください。' });
    return title;
}
function validateBody(value: string): void {
    if ([...value].length > 10000)
        throw new Fault('VALIDATION', '本文は10,000文字以内で入力してください。', { body: '10,000文字以内で入力してください。' });
}
function translateDatabaseError(error: unknown): unknown {
    if (error && typeof error === 'object' && 'errcode' in error && error.errcode === 2067) {
        return new Fault('TITLE_EXISTS', '同じタイトルのメモが既にあります。', { title: '別のタイトルを指定してください。' });
    }
    return error;
}

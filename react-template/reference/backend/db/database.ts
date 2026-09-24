import { DatabaseSync } from 'node:sqlite';
import { mkdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
export function openDatabase(path: string): DatabaseSync {
    if (path === ':memory:')
        throw new Error('このアプリはファイルDBを使用します。テストも専用ファイルを指定してください。');
    const absolute = resolve(path);
    mkdirSync(dirname(absolute), { recursive: true });
    const db = new DatabaseSync(absolute);
    try {
        db.exec('PRAGMA foreign_keys=ON; PRAGMA busy_timeout=2000; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;');
        migrate(db);
        return db;
    }
    catch (error) {
        db.close();
        throw error;
    }
}
function migrate(db: DatabaseSync): void {
    // 既存DBの上書き初期化や、未知の将来版のダウングレードはしない。
    db.exec('BEGIN IMMEDIATE');
    try {
        const version = Number(db.prepare('PRAGMA user_version').get()?.user_version);
        if (version > 1 || version < 0 || !Number.isInteger(version))
            throw new Error('未対応のDBスキーマです。');
        if (version === 0) {
            db.exec(readFileSync(new URL('./migrations/001-notes.sql', import.meta.url), 'utf8'));
            db.exec('PRAGMA user_version=1');
        }
        db.exec('COMMIT');
    }
    catch (error) {
        db.exec('ROLLBACK');
        throw error;
    }
}

import test from 'node:test';
import assert from 'node:assert/strict';
import { DatabaseSync } from 'node:sqlite';
import { mkdtempSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { spawnSync } from 'node:child_process';
import { openDatabase } from '../../backend/db/database.ts';
import { fixture } from './fixture.ts';
test('本番と同じmigration、WAL、外部キーを適用する', () => {
    const f = fixture();
    try {
        assert.equal(f.db.prepare('PRAGMA user_version').get()?.user_version, 1);
        assert.equal(f.db.prepare('PRAGMA journal_mode').get()?.journal_mode, 'wal');
        assert.equal(f.db.prepare('PRAGMA foreign_keys').get()?.foreign_keys, 1);
        assert.throws(() => f.db.prepare('INSERT INTO notes VALUES(?,?,?,?,?,?)').run('n', 'missing', 'a', '', 1, 'time'));
    }
    finally {
        f.close();
    }
});
test('別の読取専用接続からコミット済み結果を確認できる', () => {
    const f = fixture();
    try {
        f.notes.save('alice', { title: 'persist', body: 'disk' });
        const other = new DatabaseSync(f.path, { readOnly: true });
        try {
            assert.equal(other.prepare('SELECT body FROM notes').get()?.body, 'disk');
        }
        finally {
            other.close();
        }
    }
    finally {
        f.close();
    }
});
test('接続を閉じて開き直してもmigrationはデータを消さない', () => {
    const dir = mkdtempSync(join(tmpdir(), 'aidd-migrate-'));
    const path = join(dir, 'db.sqlite');
    try {
        let db = openDatabase(path);
        db.prepare('INSERT INTO users VALUES(?,?)').run('id', 'name');
        db.close();
        db = openDatabase(path);
        assert.equal(db.prepare('SELECT name FROM users').get()?.name, 'name');
        db.close();
    }
    finally {
        rmSync(dir, { recursive: true, force: true });
    }
});
test('未知のスキーマ版は拒否し、初期化しない', () => {
    const dir = mkdtempSync(join(tmpdir(), 'aidd-version-'));
    const path = join(dir, 'db.sqlite');
    try {
        const db = openDatabase(path);
        db.exec('PRAGMA user_version=99');
        db.close();
        assert.throws(() => openDatabase(path), /未対応/);
        const readonly = new DatabaseSync(path, { readOnly: true });
        assert.equal(readonly.prepare('PRAGMA user_version').get()?.user_version, 99);
        readonly.close();
    }
    finally {
        rmSync(dir, { recursive: true, force: true });
    }
});
test('稼働中のWALから整合したバックアップを作成できる', () => {
    const f = fixture();
    try {
        f.notes.save('alice', { title: 'backup', body: 'copy' });
        const target = join(f.dir, 'backup.sqlite');
        const child = spawnSync(process.execPath, ['--experimental-strip-types', resolve('scripts/backup.ts'), target], { env: { ...process.env, DB_PATH: f.path }, encoding: 'utf8' });
        assert.equal(child.status, 0, child.stderr);
        const db = new DatabaseSync(target, { readOnly: true });
        try {
            assert.equal(db.prepare('PRAGMA quick_check').get()?.quick_check, 'ok');
            assert.equal(db.prepare('SELECT body FROM notes').get()?.body, 'copy');
        }
        finally {
            db.close();
        }
    }
    finally {
        f.close();
    }
});

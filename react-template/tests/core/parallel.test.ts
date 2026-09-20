import test from 'node:test';
import assert from 'node:assert/strict';
import { fork } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { DatabaseSync } from 'node:sqlite';
test('4つの実Nodeプロセスで同じキーを書き込んでも独立DBで干渉しない', async () => {
    const root = mkdtempSync(join(tmpdir(), 'aidd-parallel-'));
    const children = Array.from({ length: 4 }, (_, i) => fork(resolve('tests/core/parallel-worker.ts'), [join(root, `worker-${i}/app.sqlite`)], { execArgv: ['--experimental-strip-types'], stdio: ['ignore', 'ignore', 'ignore', 'ipc'] }));
    try {
        const ready = children.map(child => new Promise<number>((resolveReady, reject) => {
            const timer = setTimeout(() => reject(new Error('起動期限')), 15000);
            child.once('error', reject);
            child.on('message', (m: {
                type: string;
                pid: number;
            }) => {
                if (m.type === 'ready') {
                    clearTimeout(timer);
                    resolveReady(m.pid);
                }
            });
        }));
        const done = children.map(child => new Promise<{
            version: number;
            count: number;
        }>((resolveDone, reject) => {
            const timer = setTimeout(() => reject(new Error('保存期限')), 20000);
            child.on('message', (m: {
                type: string;
                version: number;
                count: number;
            }) => {
                if (m.type === 'done') {
                    clearTimeout(timer);
                    resolveDone(m);
                }
            });
            child.once('error', reject);
            child.once('exit', code => {
                if (code !== 0)
                    reject(new Error(`worker exit ${code}`));
            });
        }));
        const pids = await Promise.all(ready);
        assert.equal(new Set(pids).size, 4);
        // 全プロセスが準備完了してから開始。成功数だけでなく各ファイルを別接続で検証する。
        for (const child of children)
            child.send('start');
        for (const value of await Promise.all(done)) {
            assert.equal(value.version, 20);
            assert.equal(value.count, 1);
        }
        await Promise.all(children.map(c => c.exitCode !== null ? Promise.resolve() : new Promise<void>(r => c.once('exit', () => r()))));
        for (let i = 0; i < 4; i++) {
            const db = new DatabaseSync(join(root, `worker-${i}/app.sqlite`), { readOnly: true });
            try {
                const row = db.prepare('SELECT title,body,version FROM notes').get();
                assert.equal(row?.title, 'parallel-fixed-title');
                assert.equal(row?.version, 20);
                assert.equal(row?.body, '19');
            }
            finally {
                db.close();
            }
        }
    }
    finally {
        for (const child of children)
            if (child.exitCode === null)
                child.kill();
        rmSync(root, { recursive: true, force: true, maxRetries: 6, retryDelay: 100 });
    }
});

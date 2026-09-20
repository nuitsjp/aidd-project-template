import test from 'node:test';
import assert from 'node:assert/strict';
import { fixture } from './fixture.ts';
import { Fault } from '../../backend/fault.ts';
test('作成・編集・削除をDBへ確定する', () => {
    const f = fixture();
    try {
        const a = f.notes.save('alice', { title: '対象', body: 'initial' });
        assert.equal(a.version, 1);
        const b = f.notes.save('alice', { ...a, body: 'updated' });
        assert.equal(b.version, 2);
        assert.equal(f.notes.list('alice')[0]?.body, 'updated');
        f.notes.remove('alice', b.id, b.version);
        assert.deepEqual(f.notes.list('alice'), []);
    }
    finally {
        f.close();
    }
});
test('不正入力ではDBを書かない', () => {
    const f = fixture();
    try {
        for (const title of ['', '  ', 'a'.repeat(101)])
            assert.throws(() => f.notes.save('alice', { title, body: '' }), (e: unknown) => e instanceof Fault && e.code === 'VALIDATION');
        assert.equal(f.notes.list('alice').length, 0);
    }
    finally {
        f.close();
    }
});
test('Unicodeの100文字境界をDBと一致させる', () => {
    const f = fixture();
    try {
        assert.equal([...f.notes.save('alice', { title: '😀'.repeat(100), body: '' }).title].length, 100);
        assert.throws(() => f.notes.save('alice', { title: '😀'.repeat(101), body: '' }));
    }
    finally {
        f.close();
    }
});
test('利用者を跨ぐ取得・更新・削除を拒否する', () => {
    const f = fixture();
    try {
        const a = f.notes.save('alice', { title: '同じキー', body: 'private' });
        f.notes.save('bob', { title: '同じキー', body: 'other' });
        assert.throws(() => f.notes.get('bob', a.id), (e: unknown) => e instanceof Fault && e.code === 'NOT_FOUND');
        assert.throws(() => f.notes.save('bob', { ...a, body: 'attack' }));
        assert.throws(() => f.notes.remove('bob', a.id, a.version));
        assert.equal(f.notes.get('alice', a.id).body, 'private');
        assert.equal(f.notes.list('bob')[0]?.body, 'other');
    }
    finally {
        f.close();
    }
});
test('古い下書きを検出して勝手に上書きしない', () => {
    const f = fixture();
    try {
        const a = f.notes.save('alice', { title: 'edit', body: 'v1' });
        f.notes.save('alice', { ...a, body: 'v2' });
        assert.throws(() => f.notes.save('alice', { ...a, body: 'stale' }), (e: unknown) => e instanceof Fault && e.code === 'EDIT_CONFLICT');
        assert.equal(f.notes.get('alice', a.id).body, 'v2');
    }
    finally {
        f.close();
    }
});
test('通知は結果確定後、対象利用者だけへ発行する', () => {
    const f = fixture();
    try {
        let alice = 0, bob = 0;
        const offA = f.notes.subscribe('alice', () => { alice++; assert.equal(f.notes.list('alice').length, 1); });
        const offB = f.notes.subscribe('bob', () => bob++);
        f.notes.save('alice', { title: 'notify', body: '' });
        assert.equal(alice, 1);
        assert.equal(bob, 0);
        offA();
        offB();
        f.notes.save('alice', { title: 'other', body: '' });
        assert.equal(alice, 1);
    }
    finally {
        f.close();
    }
});
test('通知の障害を確定済みの保存失敗と混同しない', () => {
    const f = fixture();
    try {
        const off = f.notes.subscribe('alice', () => { throw new Error('notification failed'); });
        const note = f.notes.save('alice', { title: 'committed', body: '' });
        assert.equal(f.notes.get('alice', note.id).version, 1);
        assert.equal(f.notificationErrors.length, 1);
        off();
    }
    finally {
        f.close();
    }
});

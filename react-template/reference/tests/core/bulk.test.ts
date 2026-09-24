import test from 'node:test';
import assert from 'node:assert/strict';
import { fixture } from './fixture.ts';
import { Fault } from '../../backend/fault.ts';
test('プレビューは保存せず、確認後の操作で全件確定する', () => {
    const f = fixture();
    try {
        const input = { titles: 'a\nb', body: 'shared' };
        assert.deepEqual(f.notes.preview(input).titles, ['a', 'b']);
        assert.equal(f.notes.list('alice').length, 0);
        assert.deepEqual(f.notes.importMany('alice', input), { count: 2 });
        assert.equal(f.notes.list('alice').length, 2);
    }
    finally {
        f.close();
    }
});
test('2件目で一意制約違反なら先行INSERTもロールバックする', () => {
    const f = fixture();
    try {
        f.notes.save('alice', { title: 'already', body: 'retained' });
        let notified = 0;
        const off = f.notes.subscribe('alice', () => notified++);
        assert.throws(() => f.notes.importMany('alice', { titles: 'inserted-first\nalready', body: '' }), (e: unknown) => e instanceof Fault && e.code === 'TITLE_EXISTS');
        assert.deepEqual(f.notes.list('alice').map(n => n.title), ['already']);
        assert.equal(notified, 0);
        off();
    }
    finally {
        f.close();
    }
});
test('予期しないDBエラーでも部分更新を残さない', () => {
    const f = fixture();
    try {
        f.db.exec("CREATE TRIGGER test_reject BEFORE INSERT ON notes WHEN NEW.title='reject' BEGIN SELECT RAISE(ABORT,'simulated storage failure'); END;");
        assert.throws(() => f.notes.importMany('alice', { titles: 'accepted-first\nreject', body: '' }));
        assert.equal(f.notes.list('alice').length, 0);
    }
    finally {
        f.close();
    }
});
test('重複・件数上限・本文長を検証する', () => {
    const f = fixture();
    try {
        for (const input of [{ titles: 'a\na', body: '' }, { titles: '', body: '' }, { titles: Array.from({ length: 101 }, (_, i) => '' + i).join('\n'), body: '' }, { titles: 'a', body: 'x'.repeat(10001) }])
            assert.throws(() => f.notes.importMany('alice', input));
        assert.equal(f.notes.list('alice').length, 0);
    }
    finally {
        f.close();
    }
});

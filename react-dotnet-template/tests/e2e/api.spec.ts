import { test, expect } from './fixtures.ts';

// HTTP契約はAPIへ直接接続して検証し、ブラウザのシナリオではdev時にViteを経由する。
test.use({ serveFrontend: false });

test('HTTP契約はcamelCaseで保存結果を返し、他者の取得を拒否する', async ({ request, app }) => {
    const headers = { Origin: app.url };
    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'alice' } })).ok()).toBe(true);
    const response = await request.post('/api/notes/save', { headers, data: { title: ' 契約の確認 ', body: '本文' } });
    expect(response.status()).toBe(200);
    const note = await response.json();
    expect(Object.keys(note).sort()).toEqual(['body', 'id', 'title', 'updatedAt', 'version']);
    expect(note).toMatchObject({ title: '契約の確認', body: '本文', version: 1 });
    expect(note.id).toMatch(/^[0-9a-f-]{36}$/i);
    expect(Number.isNaN(Date.parse(note.updatedAt))).toBe(false);
    expect(await (await request.get('/api/notes')).json()).toEqual([note]);
    expect(app.rows()).toEqual([{ owner_id: 'alice', title: '契約の確認', body: '本文', version: 1 }]);

    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'bob' } })).ok()).toBe(true);
    const other = await request.get(`/api/notes/${note.id}`);
    expect(other.status()).toBe(404);
    expect(await other.json()).toMatchObject({ status: 404, detail: '対象のメモが見つかりません。' });
    expect(await (await request.get('/api/notes')).json()).toEqual([]);
});

test('HTTP境界は不正なJSONと入力形状を拒否しDBを変更しない', async ({ request, app }) => {
    const headers = { Origin: app.url };
    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'alice' } })).ok()).toBe(true);
    for (const input of [null, {}, { title: '本文欠落' }, { title: 123, body: '' },
        { title: null, body: '' }, { title: 'null本文', body: null },
        { title: '明示null ID', body: '', id: null, version: null },
        { title: '明示null 版', body: '', id: '00000000-0000-4000-8000-000000000001', version: null },
        { title: '明示null IDのみ', body: '', id: null, version: 1 },
        { title: '余分な項目', body: '', ownerId: 'bob' },
        { title: '大小文字', body: '', Title: '別名' },
        { title: '版文字列', body: '', id: '00000000-0000-4000-8000-000000000001', version: '1' },
        { title: '不正ID', body: '', id: 'not-a-uuid', version: 1 },
        { title: '不正版', body: '', id: '00000000-0000-4000-8000-000000000001', version: 0 },
        { title: '版欠落', body: '', id: '00000000-0000-4000-8000-000000000001' },
        { title: 'ID欠落', body: '', version: 1 }]) {
        const response = await request.post('/api/notes/save', {
            headers: { ...headers, 'Content-Type': 'application/json' }, data: JSON.stringify(input),
        });
        expect(response.status(), JSON.stringify(input)).toBe(400);
        expect(await response.json()).toMatchObject({ status: 400, errors: expect.any(Object) });
    }
    const duplicate = await request.post('/api/notes/save', {
        headers: { ...headers, 'Content-Type': 'application/json' },
        data: '{"title":"重複キー","body":"","title":"重複キー"}',
    });
    expect(duplicate.status()).toBe(400);
    expect(await duplicate.json()).toMatchObject({ status: 400, errors: expect.any(Object) });
    const multiple = await request.post('/api/notes/save', {
        headers,
        data: { title: '   ', body: 'x'.repeat(10_001) },
    });
    expect(multiple.status()).toBe(400);
    expect(multiple.headers()['content-type']).toContain('application/problem+json');
    expect(await multiple.json()).toMatchObject({
        status: 400,
        errors: {
            Title: ['タイトルは1〜100文字で入力してください。'],
            Body: ['本文は10,000文字以内で入力してください。'],
        },
    });
    const broken = await request.post('/api/notes/save', {
        headers: { ...headers, 'Content-Type': 'application/json' }, data: '{',
    });
    expect(broken.status()).toBe(400);
    expect(await broken.json()).toMatchObject({ status: 400, errors: expect.any(Object) });
    const oversized = await request.post('/api/notes/save', {
        headers, data: { title: '上限超過', body: 'x'.repeat(1024 * 1024) },
    });
    expect(oversized.status()).toBe(413);
    expect(await oversized.json()).toMatchObject({ status: 413, detail: 'リクエストが大きすぎます。' });
    expect(app.rows()).toEqual([]);
});

test('削除入力のUUIDと版を検証し、不正入力ではDBを変更しない', async ({ request, app }) => {
    const headers = { Origin: app.url };
    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'alice' } })).ok()).toBe(true);
    const saved = await request.post('/api/notes/save', {
        headers, data: { title: '削除しないメモ', body: '本文' },
    });
    expect(saved.status()).toBe(200);
    const note = await saved.json();

    for (const { input, field } of [
        { input: { id: 'not-a-uuid', version: note.version }, field: 'Id' },
        { input: { id: note.id, version: 0 }, field: 'Version' },
    ]) {
        const response = await request.post('/api/notes/remove', { headers, data: input });
        expect(response.status(), JSON.stringify(input)).toBe(400);
        expect(await response.json()).toMatchObject({
            status: 400,
            errors: { [field]: expect.any(Array) },
        });
    }
    expect(app.rows()).toEqual([{
        owner_id: 'alice', title: '削除しないメモ', body: '本文', version: 1,
    }]);
});

test('HTTP境界は未認証操作と異なるoriginからの更新を拒否する', async ({ request, app }) => {
    expect(await (await request.get('/api/session')).json()).toEqual({ user: null, mode: 'demo' });
    const anonymous = await request.get('/api/notes');
    expect(anonymous.status()).toBe(401);
    expect(await anonymous.json()).toMatchObject({ status: 401, detail: '利用者を確認できません。' });
    const headers = { Origin: app.url };
    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'alice' } })).ok()).toBe(true);
    const foreign = await request.post('/api/notes/save', {
        headers: { Origin: 'https://other.example' }, data: { title: '保存しない', body: '' },
    });
    expect(foreign.status()).toBe(403);
    const missingOrigin = await request.post('/api/notes/save', { data: { title: '保存しない', body: '' } });
    expect(missingOrigin.status()).toBe(403);
    expect(app.rows()).toEqual([]);
});

test('同一.NETサーバーがSPAを配信し、未知APIにはHTMLを返さない', async ({ request, app }) => {
    test.skip(app.mode !== 'hosted', 'SPAの一体配信はhostedモードだけで検証します。');
    const page = await request.get('/import/confirm', { headers: { Accept: 'text/html' } });
    expect(page.status()).toBe(200);
    expect(page.headers()['content-type']).toContain('text/html');
    expect(await page.text()).toContain('<div id="root">');
    for (const path of ['/api/not-found', '/events/not-found']) {
        const response = await request.get(path, { headers: { Accept: 'text/html' } });
        expect(response.status()).toBe(404);
        expect(response.headers()['content-type'] ?? '').not.toContain('text/html');
    }
});

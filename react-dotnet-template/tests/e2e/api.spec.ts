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
    expect(await other.json()).toMatchObject({ appError: { code: 'NOT_FOUND' } });
    expect(await (await request.get('/api/notes')).json()).toEqual([]);
});

test('HTTP境界は不正なJSONと入力形状を拒否しDBを変更しない', async ({ request, app }) => {
    const headers = { Origin: app.url };
    expect((await request.post('/api/demo/sign-in', { headers, data: { user: 'alice' } })).ok()).toBe(true);
    for (const input of [null, {}, { title: '本文欠落' }, { title: 123, body: '' },
        { title: '余分な項目', body: '', ownerId: 'bob' },
        { title: '不正ID', body: '', id: 'not-a-uuid', version: 1 },
        { title: '不正版', body: '', id: '00000000-0000-4000-8000-000000000001', version: 0 }]) {
        const response = await request.post('/api/notes/save', {
            headers: { ...headers, 'Content-Type': 'application/json' }, data: JSON.stringify(input),
        });
        expect(response.status(), JSON.stringify(input)).toBe(400);
        expect(await response.json()).toMatchObject({ appError: { code: 'VALIDATION' } });
    }
    const broken = await request.post('/api/notes/save', {
        headers: { ...headers, 'Content-Type': 'application/json' }, data: '{',
    });
    expect(broken.status()).toBe(400);
    expect(await broken.json()).toMatchObject({ appError: { code: 'VALIDATION' } });
    const oversized = await request.post('/api/notes/save', {
        headers, data: { title: '上限超過', body: 'x'.repeat(1024 * 1024) },
    });
    expect(oversized.status()).toBe(413);
    expect(await oversized.json()).toMatchObject({ appError: { code: 'VALIDATION' } });
    expect(app.rows()).toEqual([]);
});

test('HTTP境界は未認証操作と異なるoriginからの更新を拒否する', async ({ request, app }) => {
    expect(await (await request.get('/api/session')).json()).toEqual({ user: null, mode: 'demo' });
    const anonymous = await request.get('/api/notes');
    expect(anonymous.status()).toBe(401);
    expect(await anonymous.json()).toMatchObject({ appError: { code: 'UNAUTHENTICATED' } });
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

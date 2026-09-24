import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';

test('別ユーザーによる参照・更新・削除', async ({ page, app, browser }) => {
  const other = await browser.newContext({ baseURL: app.url });
  try {
    const bob = await other.newPage();
    let note = { id: '', version: 0 };
    await test.step('分岐条件', async () => {
      await signIn(page);
      await saveFromUI(page, '所有者別タイトル', 'Aliceの本文');
      [note] = await (await page.request.get('/api/notes')).json();
      await signIn(bob, 'Bob');
    });
    await test.step('手順1', async () => {
      const headers = { Origin: app.url };
      const read = await bob.request.get(`/api/notes/${note.id}`);
      const update = await bob.request.post('/api/notes/save', {
        headers,
        data: { id: note.id, version: note.version, title: '奪取', body: 'Bobの本文' },
      });
      const remove = await bob.request.post('/api/notes/remove', {
        headers,
        data: { id: note.id, version: note.version },
      });
      await expect(bob.getByRole('button', { name: '所有者別タイトルを編集' })).toHaveCount(0);
      expect([read.status(), update.status(), remove.status()]).toEqual([404, 404, 404]);
    });
    await test.step('受け入れ条件', async () => {
      expect(app.rows()).toEqual([
        { owner_id: 'alice', title: '所有者別タイトル', body: 'Aliceの本文', version: 1 },
      ]);
    });
  } finally {
    await other.close();
  }
});

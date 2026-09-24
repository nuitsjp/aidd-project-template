import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';
test('別ユーザーのデータは表示されない', async ({ page, context, app, browser }) => {
    const other = await browser.newContext({ baseURL: app.url });
    try {
        const bob = await other.newPage();
        await test.step('分岐条件', async () => {
            await signIn(page);
            await saveFromUI(page, '所有者別タイトル', 'Aliceの本文');
            await signIn(bob, 'Bob');
        });
        await test.step('手順1', async () => {
            await expect(bob.getByRole('button', { name: '所有者別タイトルを編集' })).toHaveCount(0);
            await saveFromUI(bob, '所有者別タイトル', 'Bobの本文');
        });
        await test.step('受け入れ条件', async () => {
            expect(app.rows().map(r => r.owner_id).sort()).toEqual(['alice', 'bob']);
            // 同一BrowserContextは使わずCookieも分離する。
            expect((await context.cookies()).length).toBeGreaterThan(0);
        });
    }
    finally {
        await other.close();
    }
});

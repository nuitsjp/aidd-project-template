import { test, expect } from './fixtures.ts';
import { signIn, saveFromUI } from './helpers.ts';
test('メモを作成・編集して保存する / 他者のメモへのアクセスを拒否する 別ユーザーのデータは表示されない', async ({ page, context, app, browser }) => {
    await signIn(page);
    await saveFromUI(page, '所有者別タイトル', 'Aliceの本文');
    const other = await browser.newContext({ baseURL: app.url });
    try {
        const bob = await other.newPage();
        await signIn(bob, 'Bob');
        await expect(bob.getByRole('button', { name: '所有者別タイトルを編集' })).toHaveCount(0);
        await saveFromUI(bob, '所有者別タイトル', 'Bobの本文');
        expect(app.rows().map(r => r.owner_id).sort()).toEqual(['alice', 'bob']);
        // 同一BrowserContextは使わずCookieも分離する。
        expect((await context.cookies()).length).toBeGreaterThan(0);
    }
    finally {
        await other.close();
    }
});
test('メモを作成・編集して保存する / 更新競合で下書きを維持する 同じDBの二つの対話では古い版の上書きを拒否する', async ({ page, context, app }) => {
    await signIn(page);
    await saveFromUI(page, '競合対象', '初期');
    const second = await context.newPage();
    await second.goto('/notes');
    await second.getByRole('button', { name: '競合対象を編集' }).click();
    await second.getByLabel('本文', { exact: true }).fill('残す競合下書き');
    await page.getByLabel('本文', { exact: true }).fill('先に確定');
    await page.getByRole('button', { name: '保存する', exact: true }).click();
    await expect.poll(() => app.rows()[0]?.version).toBe(2);
    await second.getByRole('button', { name: '保存する', exact: true }).click();
    await expect(second.getByRole('alert')).toContainText('EDIT_CONFLICT');
    await expect(second.getByLabel('本文', { exact: true })).toHaveValue('残す競合下書き');
    expect(app.rows()[0]?.body).toBe('先に確定');
    await second.close();
});
test('メモを作成・編集して保存する / メモを作成・編集して保存する 別タブの確定をSSEで一覧へ反映する', async ({ page, context, app }) => {
    await signIn(page);
    const second = await context.newPage();
    await second.goto('/notes');
    await expect(second.getByLabel('通知接続')).toHaveText('変更通知 接続済み');
    await saveFromUI(page, '通知を確認', 'DB確定済み');
    await expect(second.getByRole('button', { name: '通知を確認を編集' })).toBeVisible();
    expect(app.rows()).toHaveLength(1);
    await second.close();
});

import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';
test('既存メモの編集がSQLiteへ確定し、別タブと再起動後も保持される', async ({ page, context, app }) => {
    const second = await context.newPage();
    await test.step('開始条件', async () => {
        await signIn(page);
        await saveFromUI(page, '同じタイトル', '初期本文');
        await expect.poll(() => app.rows()).toEqual([{ owner_id: 'alice', title: '同じタイトル', body: '初期本文', version: 1 }]);
        await second.goto('/notes');
        await expect(second.getByLabel('通知接続')).toHaveText('変更通知 接続済み');
    });
    await test.step('手順1', async () => {
        await page.reload();
        await expect(page.getByRole('heading', { name: '保存済みのメモ' })).toBeVisible();
        await expect(page.getByRole('button', { name: '同じタイトルを編集', exact: true })).toBeVisible();
    });
    await test.step('手順2', async () => {
        await page.getByRole('button', { name: '同じタイトルを編集', exact: true }).click();
        await expect(page.getByLabel('タイトル', { exact: true })).toHaveValue('同じタイトル');
        await expect(page.getByLabel('本文', { exact: true })).toHaveValue('初期本文');
        await page.getByLabel('本文', { exact: true }).fill('更新本文');
    });
    await test.step('手順3', async () => {
        await page.getByRole('button', { name: '保存する', exact: true }).click();
        await expect(page.getByRole('status').filter({ hasText: '保存しました' })).toBeVisible();
        await expect(page.getByText('v2 ·')).toBeVisible();
    });
    await test.step('受け入れ条件', async () => {
        expect(app.rows()).toEqual([{ owner_id: 'alice', title: '同じタイトル', body: '更新本文', version: 2 }]);
        await expect(second.getByText('v2 ·')).toBeVisible();
        await second.close();
        await app.restart();
        await page.goto(app.url + '/notes');
        await page.getByRole('button', { name: 'Aliceで開始' }).click();
        await page.getByRole('button', { name: '同じタイトルを編集', exact: true }).click();
        await expect(page.getByLabel('本文', { exact: true })).toHaveValue('更新本文');
        expect(app.rows()[0]?.version).toBe(2);
    });
});

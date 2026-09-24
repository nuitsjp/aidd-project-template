import { test, expect } from '../../fixtures.ts';
import { signIn } from '../../helpers.ts';
test('確認前は未保存、確認後に全件をSQLiteへ登録する', async ({ page, app }) => {
    await test.step('開始条件', async () => {
        await signIn(page);
        await page.getByRole('link', { name: /一括登録/ }).click();
    });
    await test.step('手順1', async () => {
        await page.getByLabel('タイトル一覧').fill('一件目\n二件目');
        await page.getByLabel('共通の本文').fill('一括本文');
        await page.getByRole('button', { name: '内容を確認する' }).click();
        await expect(page.getByRole('heading', { name: '2. 内容を確認して保存' })).toBeVisible();
        expect(app.rows()).toEqual([]);
    });
    await test.step('手順2', async () => {
        await page.getByRole('button', { name: '一括登録する', exact: true }).click();
        await expect(page.getByRole('status').filter({ hasText: '2件を登録しました' })).toBeVisible();
    });
    await test.step('受け入れ条件', async () => {
        expect(app.rows().map(r => r.body)).toEqual(['一括本文', '一括本文']);
        await page.getByRole('button', { name: 'メモ一覧へ' }).click();
        await expect(page.getByRole('button', { name: '一件目を編集' })).toBeVisible();
    });
});

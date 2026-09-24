import { test, expect } from '../../fixtures.ts';
import { signIn } from '../../helpers.ts';
test('不正入力ではDBを更新せず下書きを保持する', async ({ page, app }) => {
    await test.step('分岐条件', async () => {
        await signIn(page);
        await page.getByLabel('タイトル', { exact: true }).fill('   ');
        await page.getByLabel('本文', { exact: true }).fill('残す下書き');
    });
    await test.step('手順1', async () => {
        await page.getByRole('button', { name: '保存する', exact: true }).click();
        await expect(page.getByRole('alert')).toContainText('VALIDATION');
        await expect(page.getByLabel('本文', { exact: true })).toHaveValue('残す下書き');
    });
    await test.step('受け入れ条件', () => {
        expect(app.rows()).toHaveLength(0);
    });
});

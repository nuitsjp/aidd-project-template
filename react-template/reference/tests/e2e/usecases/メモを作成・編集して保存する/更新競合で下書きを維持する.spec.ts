import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';
test('同じDBの二つの対話では古い版の上書きを拒否する', async ({ page, context, app }) => {
    const second = await context.newPage();
    await test.step('分岐条件', async () => {
        await signIn(page);
        await saveFromUI(page, '競合対象', '初期');
        await second.goto('/notes');
        await second.getByRole('button', { name: '競合対象を編集' }).click();
        await second.getByLabel('本文', { exact: true }).fill('残す競合下書き');
        await page.getByLabel('本文', { exact: true }).fill('先に確定');
        await page.getByRole('button', { name: '保存する', exact: true }).click();
        await expect.poll(() => app.rows()[0]?.version).toBe(2);
    });
    await test.step('手順1', async () => {
        await second.getByRole('button', { name: '保存する', exact: true }).click();
        await expect(second.getByRole('alert')).toContainText('EDIT_CONFLICT');
        await expect(second.getByLabel('本文', { exact: true })).toHaveValue('残す競合下書き');
    });
    await test.step('受け入れ条件', () => {
        expect(app.rows()[0]?.body).toBe('先に確定');
    });
    await second.close();
});

import type { Dialog } from '@playwright/test';
import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';
test('確認後の削除がSQLiteへ確定する', async ({ page, app }) => {
    let clicked: Promise<void> = Promise.resolve();
    let dialog: Dialog | undefined;
    await test.step('分岐条件', async () => {
        await signIn(page);
        await saveFromUI(page, '削除対象');
    });
    await test.step('手順1', async () => {
        clicked = page.getByRole('button', { name: '削除する', exact: true }).click();
        dialog = await page.waitForEvent('dialog');
        expect(dialog.message()).toBe('このメモを削除しますか？');
    });
    await test.step('手順2', async () => {
        await dialog?.accept();
        await clicked;
        await expect.poll(() => app.rows()).toEqual([]);
    });
    await test.step('受け入れ条件', async () => {
        await page.reload();
        await expect(page.getByRole('button', { name: '削除対象を編集' })).toHaveCount(0);
    });
});

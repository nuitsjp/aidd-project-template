import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';

test('削除の確認を取り消す', async ({ page, app }) => {
  await test.step('分岐条件', async () => {
    await signIn(page);
    await saveFromUI(page, '残す対象');
    page.once('dialog', (dialog) => dialog.dismiss());
  });
  await test.step('手順1', async () => {
    await page.getByRole('button', { name: '削除する', exact: true }).click();
    await expect(page.getByRole('button', { name: '残す対象を編集' })).toBeVisible();
  });
  await test.step('受け入れ条件', async () => {
    expect(app.rows()).toEqual([
      { owner_id: 'alice', title: '残す対象', body: '本文', version: 1 },
    ]);
  });
});

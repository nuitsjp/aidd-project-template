import { test, expect } from '../../fixtures.ts';
import { signIn, saveFromUI } from '../../helpers.ts';

test('削除を確定する', async ({ page, context, app }) => {
  const second = await context.newPage();
  await test.step('分岐条件', async () => {
    await signIn(page);
    await saveFromUI(page, '削除対象');
    await second.goto('/notes');
    await expect(second.getByRole('button', { name: '削除対象を編集' })).toBeVisible();
  });
  let confirm = '';
  await test.step('手順1', async () => {
    page.once('dialog', (dialog) => {
      confirm = dialog.message();
      return dialog.accept();
    });
    await page.getByRole('button', { name: '削除する', exact: true }).click();
    expect(confirm).toBe('このメモを削除しますか？');
  });
  await test.step('手順2', async () => {
    await expect(page.getByRole('status').filter({ hasText: '削除しました' })).toBeVisible();
  });
  await test.step('受け入れ条件', async () => {
    expect(app.rows()).toEqual([]);
    await expect(second.getByRole('button', { name: '削除対象を編集' })).toHaveCount(0);
    await page.reload();
    await expect(page.getByRole('button', { name: '削除対象を編集' })).toHaveCount(0);
    await second.close();
  });
});

test('保存エラーの表示中に削除する', async ({ page, app }) => {
  await test.step('分岐条件', async () => {
    await signIn(page);
    await saveFromUI(page, '削除対象');
    await page.getByLabel('タイトル', { exact: true }).fill('   ');
    await page.getByRole('button', { name: '保存する', exact: true }).click();
    await expect(page.getByLabel('タイトル', { exact: true })).toHaveAccessibleDescription(
      'タイトルは1〜100文字で入力してください。',
    );
  });
  await test.step('手順1', async () => {
    page.once('dialog', (dialog) => dialog.accept());
    await page.getByRole('button', { name: '削除する', exact: true }).click();
  });
  await test.step('手順2', async () => {
    await expect(page.getByRole('status').filter({ hasText: '削除しました' })).toBeVisible();
  });
  await test.step('受け入れ条件', async () => {
    await expect(page.getByRole('alert')).toHaveCount(0);
    await expect(page.getByLabel('タイトル', { exact: true })).toHaveAccessibleDescription('');
    expect(app.rows()).toEqual([]);
  });
});

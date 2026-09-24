import { test, expect } from '../../fixtures.ts';
import { signIn } from '../../helpers.ts';

test('確認後にタイトル一覧を変更する', async ({ page, app }) => {
  await test.step('分岐条件', async () => {
    await signIn(page);
    await page.getByRole('link', { name: /一括登録/ }).click();
    await page.getByLabel('タイトル一覧').fill('変更前');
    await page.getByRole('button', { name: '内容を確認する' }).click();
    await expect(page.getByRole('heading', { name: '2. 内容を確認して保存' })).toBeVisible();
  });
  await test.step('手順1', async () => {
    await page.goBack();
    await page.getByLabel('タイトル一覧').fill('変更後');
    await expect(page.getByLabel('タイトル一覧')).toHaveValue('変更後');
  });
  await test.step('手順2', async () => {
    await page.goForward();
    await expect(page.getByLabel('タイトル一覧')).toHaveValue('変更後');
    await expect(page.getByRole('button', { name: '一括登録する', exact: true })).toHaveCount(0);
  });
  await test.step('受け入れ条件', async () => {
    expect(app.rows()).toEqual([]);
  });
});

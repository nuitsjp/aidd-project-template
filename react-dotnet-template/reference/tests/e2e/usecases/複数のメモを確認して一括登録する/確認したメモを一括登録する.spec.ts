import { test, expect } from '../../fixtures.ts';
import { signIn } from '../../helpers.ts';

test('二件を一括登録する', async ({ page, app }) => {
  await test.step('開始条件', async () => {
    await signIn(page);
    await page.getByRole('link', { name: /一括登録/ }).click();
  });
  await test.step('手順1', async () => {
    await page.getByLabel('タイトル一覧').fill('一件目\n二件目');
    await page.getByLabel('共通の本文').fill('一括本文');
    await page.getByRole('button', { name: '内容を確認する' }).click();
    await expect(page.getByRole('heading', { name: '2. 内容を確認して保存' })).toBeVisible();
    await expect(page.getByRole('listitem')).toHaveText(['一件目', '二件目']);
    expect(app.rows()).toEqual([]);
  });
  await test.step('手順2', async () => {
    await page.getByRole('button', { name: '一括登録する', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: '2件を登録しました' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'メモ一覧へ' })).toBeVisible();
  });
  await test.step('受け入れ条件', async () => {
    expect(app.rows()).toEqual([
      { owner_id: 'alice', title: '一件目', body: '一括本文', version: 1 },
      { owner_id: 'alice', title: '二件目', body: '一括本文', version: 1 },
    ]);
    await page.getByRole('button', { name: 'メモ一覧へ' }).click();
    await expect(page.getByRole('button', { name: '一件目を編集' })).toBeVisible();
    await expect(page.getByRole('button', { name: '二件目を編集' })).toBeVisible();
  });
});

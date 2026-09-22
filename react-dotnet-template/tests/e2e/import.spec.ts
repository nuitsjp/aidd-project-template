import { test, expect } from './fixtures.ts';
import { signIn, saveFromUI } from './helpers.ts';
test('複数のメモを確認して一括登録する / 確認したメモを一括登録する 確認前は未保存、確認後に全件をSQLiteへ登録する', async ({ page, app }) => {
    await signIn(page);
    await page.getByRole('link', { name: /一括登録/ }).click();
    await page.getByLabel('タイトル一覧').fill('一件目\n二件目');
    await page.getByLabel('共通の本文').fill('一括本文');
    await page.getByRole('button', { name: '内容を確認する' }).click();
    await expect(page.getByRole('heading', { name: '2. 内容を確認して保存' })).toBeVisible();
    expect(app.rows()).toEqual([]);
    await page.getByRole('button', { name: '一括登録する', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: '2件を登録しました' })).toBeVisible();
    expect(app.rows().map(r => r.body)).toEqual(['一括本文', '一括本文']);
    await page.getByRole('button', { name: 'メモ一覧へ' }).click();
    await expect(page.getByRole('button', { name: '一件目を編集' })).toBeVisible();
});
test('複数のメモを確認して一括登録する / 一括登録の失敗時に全件を取り消す 途中の一意制約違反は先行行もロールバックする', async ({ page, app }) => {
    await signIn(page);
    await saveFromUI(page, '既存', '変更されない');
    await page.getByRole('link', { name: /一括登録/ }).click();
    await page.getByLabel('タイトル一覧').fill('新規の先行行\n既存');
    await page.getByRole('button', { name: '内容を確認する' }).click();
    await page.getByRole('button', { name: '一括登録する', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('同じタイトルのメモが既にあります。');
    expect(app.rows()).toEqual([{ owner_id: 'alice', title: '既存', body: '変更されない', version: 1 }]);
    await page.getByRole('button', { name: '入力に戻る' }).click();
    await expect(page.getByLabel('タイトル一覧')).toHaveValue('新規の先行行\n既存');
});

import { test, expect } from './fixtures.ts';
import { signIn, saveFromUI } from './helpers.ts';
test('UC-1-M 作成・編集がUIからSQLiteへ確定する', async ({ page, app }) => {
    await signIn(page);
    await saveFromUI(page, '同じタイトル', '初期本文');
    await expect.poll(() => app.rows()).toEqual([{ owner_id: 'alice', title: '同じタイトル', body: '初期本文', version: 1 }]);
    await page.getByLabel('本文', { exact: true }).fill('更新本文');
    await page.getByRole('button', { name: '保存する', exact: true }).click();
    await expect.poll(() => app.rows()[0]?.version).toBe(2);
    await page.reload();
    await page.getByRole('button', { name: '同じタイトルを編集', exact: true }).click();
    await expect(page.getByLabel('本文', { exact: true })).toHaveValue('更新本文');
});
test('UC-1-X1 不正入力ではDBを更新せず下書きを保持する', async ({ page, app }) => {
    await signIn(page);
    await page.getByLabel('タイトル', { exact: true }).fill('   ');
    await page.getByLabel('本文', { exact: true }).fill('残す下書き');
    await page.getByRole('button', { name: '保存する', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('VALIDATION');
    expect(app.rows()).toHaveLength(0);
    await expect(page.getByLabel('本文', { exact: true })).toHaveValue('残す下書き');
});
test('UC-1-X2 削除もSQLiteへ確定する', async ({ page, app }) => {
    await signIn(page);
    await saveFromUI(page, '削除対象');
    page.once('dialog', d => d.accept());
    await page.getByRole('button', { name: '削除する', exact: true }).click();
    await expect.poll(() => app.rows()).toEqual([]);
    await page.reload();
    await expect(page.getByRole('button', { name: '削除対象を編集' })).toHaveCount(0);
});
test('UC-1-M サーバー再起動後も保存結果を読み取れる', async ({ page, app }) => {
    await signIn(page);
    await saveFromUI(page, '再起動しても保持', '永続化');
    await app.restart();
    await page.goto(app.url + '/notes');
    await page.getByRole('button', { name: 'Aliceで開始' }).click();
    await page.getByRole('button', { name: '再起動しても保持を編集' }).click();
    await expect(page.getByLabel('本文', { exact: true })).toHaveValue('永続化');
    expect(app.rows()[0]?.version).toBe(1);
});

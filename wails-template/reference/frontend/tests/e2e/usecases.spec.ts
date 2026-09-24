import { test, expect } from '@playwright/test';

test('メモを作成・編集して保存する / メモを作成・編集して保存する: save a note, reload through Go, and edit the same record', async ({ page }) => {
  await page.goto('/#/notes');
  await page.getByRole('link', { name: '新しいメモ' }).click();
  await page.getByLabel('タイトル', { exact: true }).fill('E2E 保存確認');
  await page.getByLabel('本文', { exact: true }).fill('Goが永続化する本文');
  await page.getByRole('button', { name: '保存する', exact: true }).click();
  await expect(page).toHaveURL(/id=/);
  await expect(page.getByText('保存しました。', { exact: true })).toBeVisible();
  await page.goBack();
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.goForward();
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: '新しいメモ', exact: true }).click();
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: /E2E 保存確認/ }).click();
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.reload();
  await expect(page.getByLabel('タイトル', { exact: true })).toHaveValue('E2E 保存確認');
  await expect(page.getByLabel('本文', { exact: true })).toHaveValue('Goが永続化する本文');
  await page.getByLabel('本文', { exact: true }).fill('更新した本文');
  await page.getByRole('button', { name: '保存する', exact: true }).click();
  await expect(page.getByText('保存しました。', { exact: true })).toBeVisible();
  await page.getByLabel('本文', { exact: true }).fill('一時的な変更');
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: '変更を破棄', exact: true }).click();
  await expect(page.getByText('保存しました。', { exact: true })).toHaveCount(0);
  await page.reload();
  await expect(page.getByLabel('本文', { exact: true })).toHaveValue('更新した本文');
});

test('メモを作成・編集して保存する / 保存失敗や離脱時に下書きを保護する: validation and leave guard preserve the draft', async ({ page }) => {
  await page.goto('/#/notes');
  await page.getByRole('link', { name: '新しいメモ' }).click();
  await page.getByLabel('本文', { exact: true }).fill('未保存の内容');
  await page.getByRole('button', { name: '保存する', exact: true }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByLabel('本文', { exact: true })).toHaveValue('未保存の内容');
  await page.getByRole('link', { name: '02　一括取り込み' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByText('未保存の変更を破棄して画面を移動しますか？', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: '編集に戻る', exact: true }).click();
  await expect(page.getByLabel('本文', { exact: true })).toHaveValue('未保存の内容');
});

test('CSVの内容を確認して一括登録する / CSVの内容を確認して一括登録する: keep input between routes, preview, then atomically import', async ({ page }) => {
  await page.goto('/#/import');
  const csv = 'title,body\nE2E 取込A,本文A\nE2E 取込B,本文B\n';
  await page.getByLabel('CSVデータ', { exact: true }).fill(csv);
  await page.getByRole('button', { name: '内容を確認' }).click();
  await expect(page.getByRole('cell', { name: /E2E 取込A/ })).toBeVisible();
  await page.getByRole('link', { name: '入力に戻る' }).click();
  await expect(page.getByLabel('CSVデータ', { exact: true })).toHaveValue(csv);
  await page.getByRole('button', { name: '内容を確認' }).click();
  await page.getByRole('button', { name: '取り込む', exact: true }).click();
  await expect(page.getByText('2件を取り込みました。', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: '01　メモの編集', exact: true }).click();
  await expect(page.getByRole('link', { name: /E2E 取込A/ })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('link', { name: /E2E 取込B/ })).toBeVisible();
});

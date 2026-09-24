import { describe, it, expect } from 'vitest';
import {
  HttpError,
  readErrorMessage,
  readErrorMessages,
  readFieldError,
} from '../../frontend/src/shared/errors.ts';
describe('公開エラー', () => {
  it('Problem Detailsの公開メッセージを扱う', () => {
    expect(readErrorMessage(new HttpError(409, { status: 409, detail: '重複' }))).toBe('重複');
    expect(
      readErrorMessages(
        new HttpError(400, {
          status: 400,
          errors: { title: ['タイトルを入力してください。'], body: ['本文が長すぎます。'] },
        }),
      ),
    ).toEqual(['タイトルを入力してください。', '本文が長すぎます。']);
  });
  it('入力欄で表示する項目のエラーを一覧から除く', () => {
    const error = new HttpError(400, {
      status: 400,
      errors: {
        Title: ['タイトルを入力してください。'],
        request: ['入力の形式を確認してください。'],
      },
    });
    expect(readFieldError(error, 'Title')).toBe('タイトルを入力してください。');
    expect(readFieldError(error, 'Body')).toBeUndefined();
    expect(readErrorMessages(error, ['Title'])).toEqual(['入力の形式を確認してください。']);
    expect(readErrorMessages(error, ['Title', 'request'])).toEqual([]);
  });
  it('内部エラー文字列を表示へ流さない', () => {
    expect(readErrorMessage(new Error('password=secret'))).not.toContain('secret');
  });
});

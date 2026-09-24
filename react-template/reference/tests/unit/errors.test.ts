import { describe, it, expect } from 'vitest';
import { readFault } from '../../frontend/src/shared/errors.ts';
describe('公開エラー', () => {
    it('コードと公開メッセージを扱う', () => { expect(readFault({ data: { appError: { code: 'TITLE_EXISTS', message: '重複' } } })).toEqual({ code: 'TITLE_EXISTS', message: '重複' }); });
    it('内部エラー文字列を表示へ流さない', () => { expect(readFault(new Error('password=secret')).message).not.toContain('secret'); });
});

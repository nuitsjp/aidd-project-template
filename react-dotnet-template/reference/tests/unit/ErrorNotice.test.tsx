import { render, screen } from '@testing-library/react';
import { it, expect } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { HttpError } from '../../frontend/src/shared/errors.ts';
import { ErrorNotice } from '../../frontend/src/shared/ErrorNotice.tsx';
it('入力エラーを操作の文脈で表示する', () => {
  render(
    <MantineProvider>
      <ErrorNotice
        error={
          new HttpError(400, {
            status: 400,
            errors: { title: ['タイトルを入力してください。'] },
          })
        }
      />
    </MantineProvider>,
  );
  expect(screen.getByRole('alert')).toHaveTextContent('タイトルを入力してください。');
});
it('入力欄で表示する項目のエラーだけなら表示しない', () => {
  render(
    <MantineProvider>
      <ErrorNotice
        error={
          new HttpError(400, { status: 400, errors: { Title: ['タイトルを入力してください。'] } })
        }
        fields={['Title']}
      />
    </MantineProvider>,
  );
  expect(screen.queryByRole('alert')).toBeNull();
});

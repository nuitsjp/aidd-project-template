import { render, screen } from '@testing-library/react';
import { it, expect } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { ErrorNotice } from '../../frontend/src/shared/ErrorNotice.tsx';
it('入力エラーを操作の文脈で表示する', () => {
    render(<MantineProvider>
    <ErrorNotice error={{ data: { appError: { code: 'VALIDATION', message: 'タイトルを入力してください。' } } }}/>
    </MantineProvider>);
    expect(screen.getByRole('alert')).toHaveTextContent('タイトルを入力してください。');
});

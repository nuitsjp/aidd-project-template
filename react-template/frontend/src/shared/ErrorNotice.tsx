import { Alert } from '@mantine/core';
import { readFault } from './errors.ts';
export function ErrorNotice({ error, title = '処理を完了できませんでした' }: {
    error: unknown;
    title?: string;
}) {
    if (!error)
        return null;
    const fault = readFault(error);
    return <Alert color="red" title={title} role="alert">
    <span>{fault.message}</span>
    <small style={{ display: 'block' }}>{fault.code}</small>
    </Alert>;
}

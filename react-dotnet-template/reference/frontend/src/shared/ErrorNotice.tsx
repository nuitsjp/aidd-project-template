import { Alert } from '@mantine/core';
import { readErrorMessages } from './errors.ts';
export function ErrorNotice({
  error,
  title = '処理を完了できませんでした',
}: {
  error: unknown;
  title?: string;
}) {
  if (!error) return null;
  const messages = readErrorMessages(error);
  return (
    <Alert color="red" title={title} role="alert">
      {messages.length === 1 ? (
        <span>{messages[0]}</span>
      ) : (
        <ul>
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </Alert>
  );
}

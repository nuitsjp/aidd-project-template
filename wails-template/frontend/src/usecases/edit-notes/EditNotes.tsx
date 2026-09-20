import { useState } from 'react';
import { Link, useBlocker, useNavigate } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Grid, Group, Loader, Modal, Paper, Stack, Text, Textarea, TextInput, Title, UnstyledButton } from '@mantine/core';
import type { Note } from '@bindings/wailstemplate/internal/notes/models';
import { getNote, listNotes, useSaveNote } from '../../features/notes/queries';
import { useDraftDirty } from '../../shared/ExitContext';
import { ErrorNotice } from '../../shared/ErrorNotice';
import { publicError } from '../../shared/errors';

export function EditNotes({ selectedID }: { selectedID?: string }) {
  const list = useQuery(listNotes());
  const detail = useQuery(getNote(selectedID ?? ''));
  return <Stack gap="lg">
    <div><Text c="dimmed" size="sm">UCP-1 / 取得・編集・保存</Text><Title order={1}>メモを編集する</Title><Text mt="sm" c="dimmed">編集中の内容は下書きとして保持し、保存後に一覧へ反映します。</Text></div>
    <ErrorNotice error={list.error} />
    {list.isError && <Button variant="light" onClick={() => void list.refetch()}>一覧を再取得</Button>}
    <Grid gutter="lg">
      <Grid.Col span={{ base: 12, md: 4 }}><Paper p="lg" withBorder>
        <Group justify="space-between" mb="md"><Text fw={600}>メモ一覧</Text><Text size="sm" c="dimmed">{list.data?.length ?? 0}件</Text></Group>
        <Button component={Link} to="/notes" search={{ id: undefined }} fullWidth variant="light" mb="md">新しいメモ</Button>
        {list.isPending ? <Loader size="sm" /> : <Stack gap="xs">{list.data?.length === 0 && <Text c="dimmed" size="sm">まだメモはありません。</Text>}{list.data?.map(note => <UnstyledButton key={note.id} component={Link} to="/notes" search={{ id: note.id }} p="sm" style={{ borderRadius: 6, background: selectedID === note.id ? '#e7f1fc' : undefined }}><Text fw={600} lineClamp={1}>{note.title}</Text><Text size="xs" c="dimmed">{new Date(note.updatedAt).toLocaleString('ja-JP')}</Text></UnstyledButton>)}</Stack>}
      </Paper></Grid.Col>
      <Grid.Col span={{ base: 12, md: 8 }}>
        {selectedID && detail.isPending ? <Loader /> : selectedID && detail.isError ? <ErrorNotice error={detail.error} /> : <Editor key={selectedID ?? 'new'} initial={selectedID ? detail.data : undefined} />}
      </Grid.Col>
    </Grid>
  </Stack>;
}
function Editor({ initial }: { initial?: Note }) {
  const navigate = useNavigate();
  const [base, setBase] = useState(initial);
  const [title, setTitle] = useState(initial?.title ?? '');
  const [body, setBody] = useState(initial?.body ?? '');
  const [saved, setSaved] = useState(false);
  const save = useSaveNote();
  const dirty = title !== (base?.title ?? '') || body !== (base?.body ?? '');
  useDraftDirty(dirty);
  const blocker = useBlocker({ shouldBlockFn: () => dirty || save.isPending, enableBeforeUnload: dirty || save.isPending, withResolver: true });
  const fields = save.error ? publicError(save.error).fieldErrors : undefined;
  async function commit() {
    try {
      const note = await save.mutateAsync({ id: base?.id ?? '', title, body });
      setBase(note); setTitle(note.title); setBody(note.body); setSaved(true);
      // Only this successful save may bypass the stale render's leave blocker.
      await navigate({ to: '/notes', search: { id: note.id }, ignoreBlocker: true });
    } catch { /* Mutation owns the failure; keep the user's draft. */ }
  }
  return <Paper p="xl" withBorder><Stack>
    <Group justify="space-between"><Title order={3}>{base ? 'メモの編集' : '新しいメモ'}</Title><Text size="sm" c={dirty ? 'orange' : 'dimmed'}>{dirty ? '未保存' : '保存済み / 変更なし'}</Text></Group>
    <ErrorNotice error={save.error} />
    {saved && !dirty && <Alert color="green">保存しました。</Alert>}
    <TextInput label="タイトル" value={title} onChange={event => { setTitle(event.currentTarget.value); setSaved(false); }} error={fields?.title} disabled={save.isPending} />
    <Textarea label="本文" value={body} onChange={event => { setBody(event.currentTarget.value); setSaved(false); }} error={fields?.body} minRows={10} autosize disabled={save.isPending} />
    <Group justify="flex-end"><Button variant="default" disabled={save.isPending} onClick={() => { setTitle(base?.title ?? ''); setBody(base?.body ?? ''); save.reset(); setSaved(false); }}>変更を破棄</Button><Button onClick={() => void commit()} loading={save.isPending}>保存する</Button></Group>
  </Stack>
  <Modal opened={blocker.status === 'blocked'} onClose={() => blocker.reset?.()} title="編集中の内容があります" centered>
    <Text>{save.isPending ? '保存処理の完了を待ってください。' : '未保存の変更を破棄して画面を移動しますか？'}</Text>
    <Group justify="flex-end" mt="lg"><Button variant="default" onClick={() => blocker.reset?.()}>編集に戻る</Button><Button color="red" disabled={save.isPending} onClick={() => blocker.proceed?.()}>破棄して移動</Button></Group>
  </Modal></Paper>;
}

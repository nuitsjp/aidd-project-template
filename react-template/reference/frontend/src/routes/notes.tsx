import { createFileRoute } from '@tanstack/react-router';
import { EditNotes } from '../usecases/edit-notes/EditNotes.tsx';
export const Route = createFileRoute('/notes')({ component: EditNotes });

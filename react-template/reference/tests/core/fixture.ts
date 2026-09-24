import { mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { openDatabase } from '../../backend/db/database.ts';
import { NotesService } from '../../backend/features/notes/service.ts';
import { ensureUser } from '../../backend/features/identity/users.ts';
export function fixture() {
    const dir = mkdtempSync(join(tmpdir(), 'aidd-core-'));
    const path = join(dir, 'app.sqlite');
    const db = openDatabase(path);
    ensureUser(db, { id: 'alice', name: 'Alice' });
    ensureUser(db, { id: 'bob', name: 'Bob' });
    const notificationErrors: unknown[] = [];
    const notes = new NotesService(db, error => notificationErrors.push(error));
    return { dir, path, db, notes, notificationErrors, close: () => { db.close(); rmSync(dir, { recursive: true, force: true, maxRetries: 6, retryDelay: 100 }); } };
}

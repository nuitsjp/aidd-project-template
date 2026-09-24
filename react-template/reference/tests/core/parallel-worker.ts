import { openDatabase } from '../../backend/db/database.ts';
import { NotesService } from '../../backend/features/notes/service.ts';
import { ensureUser } from '../../backend/features/identity/users.ts';
const db = openDatabase(process.argv[2]!);
ensureUser(db, { id: 'alice', name: 'Alice' });
const notes = new NotesService(db, error => { console.error(error); process.exitCode = 1; });
process.send?.({ type: 'ready', pid: process.pid });
process.once('message', () => {
    try {
        let result = notes.save('alice', { title: 'parallel-fixed-title', body: '0' });
        for (let i = 1; i < 20; i++)
            result = notes.save('alice', { ...result, body: String(i) });
        process.send?.({ type: 'done', pid: process.pid, version: result.version, count: notes.list('alice').length });
    }
    finally {
        db.close();
        process.disconnect?.();
    }
});

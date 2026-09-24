import type { DatabaseSync } from 'node:sqlite';
import type { Principal } from '../../../contracts/notes.ts';
export function ensureUser(db: DatabaseSync, user: Principal): Principal {
    db.prepare('INSERT INTO users(id,name) VALUES(?,?) ON CONFLICT(id) DO UPDATE SET name=excluded.name WHERE users.name<>excluded.name').run(user.id, user.name);
    return user;
}

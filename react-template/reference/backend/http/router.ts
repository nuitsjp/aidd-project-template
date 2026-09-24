import { initTRPC, TRPCError } from '@trpc/server';
import { ZodError } from 'zod';
import type { Principal, PublicFault } from '../../contracts/notes.ts';
import { saveNoteSchema, noteIdSchema, deleteNoteSchema, bulkSchema } from '../../contracts/schemas.ts';
import { Fault } from '../fault.ts';
import type { NotesService } from '../features/notes/service.ts';
export interface Context {
    user: Principal | null;
    notes: NotesService;
}
const t = initTRPC.context<Context>().create({
    errorFormatter({ shape, error }) {
        let fault: PublicFault;
        if (error.cause instanceof Fault)
            fault = { code: error.cause.code, message: error.cause.message, fieldErrors: error.cause.fieldErrors };
        else if (error.cause instanceof ZodError)
            fault = { code: 'VALIDATION', message: '入力の形式を確認してください。', fieldErrors: Object.fromEntries(error.cause.issues.map(i => [String(i.path[0] ?? 'form'), i.message])) };
        else if (error.code === 'UNAUTHORIZED')
            fault = { code: 'UNAUTHENTICATED', message: '利用者を確認できません。' };
        else
            fault = { code: 'INTERNAL', message: '処理を完了できませんでした。' };
        return { ...shape, message: fault.message, data: { ...shape.data, stack: undefined, appError: fault } };
    },
});
const procedure = t.procedure.use(({ ctx, next }) => {
    if (!ctx.user)
        throw new TRPCError({ code: 'UNAUTHORIZED' });
    return next({ ctx: { ...ctx, user: ctx.user } });
});
function execute<T>(action: () => T): T {
    try {
        return action();
    }
    catch (error) {
        if (error instanceof Fault) {
            const code = error.code === 'NOT_FOUND' ? 'NOT_FOUND' : error.code === 'EDIT_CONFLICT' || error.code === 'TITLE_EXISTS' ? 'CONFLICT' : 'BAD_REQUEST';
            throw new TRPCError({ code, message: error.message, cause: error });
        }
        throw new TRPCError({ code: 'INTERNAL_SERVER_ERROR', message: '処理を完了できませんでした。', cause: error });
    }
}
export const appRouter = t.router({
    notes: t.router({
        list: procedure.query(({ ctx }) => execute(() => ctx.notes.list(ctx.user.id))),
        get: procedure.input(noteIdSchema).query(({ ctx, input }) => execute(() => ctx.notes.get(ctx.user.id, input.id))),
        save: procedure.input(saveNoteSchema).mutation(({ ctx, input }) => execute(() => ctx.notes.save(ctx.user.id, input))),
        remove: procedure.input(deleteNoteSchema).mutation(({ ctx, input }) => execute(() => ctx.notes.remove(ctx.user.id, input.id, input.version))),
        preview: procedure.input(bulkSchema).mutation(({ ctx, input }) => execute(() => ctx.notes.preview(input))),
        importMany: procedure.input(bulkSchema).mutation(({ ctx, input }) => execute(() => ctx.notes.importMany(ctx.user.id, input))),
    }),
});
export type AppRouter = typeof appRouter;

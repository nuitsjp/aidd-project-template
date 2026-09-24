import { z } from 'zod';
// 業務上の長さ・重複条件は機能Serviceで保証する。ここでは外部入力の形を検証する。
export const saveNoteSchema = z.object({ id: z.string().uuid().optional(), version: z.number().int().positive().optional(), title: z.string(), body: z.string() }).strict();
export const noteIdSchema = z.object({ id: z.string().uuid() }).strict();
export const deleteNoteSchema = noteIdSchema.extend({ version: z.number().int().positive() });
export const bulkSchema = z.object({ titles: z.string(), body: z.string() }).strict();

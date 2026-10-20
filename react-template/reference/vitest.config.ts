import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
const local = (path: string) => fileURLToPath(new URL(path, import.meta.url));
// 画面と同じ参照名で本番のモジュールを解決し、テストごとにvi.mockで差し替える。
export default defineConfig({
  plugins: [react()],
  define: { __MOCK__: 'false' },
  resolve: {
    alias: { '@notes-access': local('./frontend/src/features/notes/access.ts') },
  },
  test: {
    environment: 'jsdom',
    include: ['tests/unit/**/*.test.{ts,tsx}'],
    setupFiles: ['tests/unit/setup.ts'],
  },
});

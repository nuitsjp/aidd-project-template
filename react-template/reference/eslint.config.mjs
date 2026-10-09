import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
export default tseslint.config(
  {
    ignores: [
      'dist/**',
      'frontend/dist/**',
      'frontend/src/routeTree.gen.ts',
      'node_modules/**',
      '.e2e-results/**',
      'playwright-report/**',
      'release/**',
    ],
  },
  js.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    extends: [...tseslint.configs.recommended],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.json', './tsconfig.backend.json', './tsconfig.tests.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/no-floating-promises': ['error', { ignoreVoid: false }],
      '@typescript-eslint/no-misused-promises': 'error',
      '@typescript-eslint/await-thenable': 'error',
    },
  },
  {
    languageOptions: { globals: { ...globals.node, ...globals.browser, __MOCK__: 'readonly' } },
    rules: { 'no-void': ['error', { allowAsStatement: true }] },
  },
  {
    files: ['frontend/src/**/*.{ts,tsx}'],
    plugins: { 'react-hooks': reactHooks },
    rules: { ...reactHooks.configs.recommended.rules, 'react-hooks/exhaustive-deps': 'error' },
  },
  {
    files: ['frontend/src/usecases/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/backend/**', '**/features/client*', '@trpc/client'],
              message: '操作は機能アクセスを経由する。',
            },
          ],
        },
      ],
    },
  },
  {
    files: ['frontend/src/features/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/usecases/**', '**/routes/**'],
              message: '機能アクセスは画面に依存しない。',
            },
          ],
        },
      ],
    },
  },
  {
    files: ['frontend/src/shared/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/features/**', '**/usecases/**'],
              message: '共用UIに機能呼び出しを置かない。',
            },
          ],
        },
      ],
    },
  },
);

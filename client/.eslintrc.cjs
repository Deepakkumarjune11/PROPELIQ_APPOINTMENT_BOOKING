module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react/recommended',
    'plugin:react-hooks/recommended',
    'plugin:jsx-a11y/recommended',
    // Must be last — disables ESLint rules that conflict with Prettier
    'prettier',
  ],
  ignorePatterns: ['dist', '.eslintrc.cjs'],
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
  },
  plugins: ['react-refresh', '@typescript-eslint', 'jsx-a11y', 'import'],
  settings: {
    // Auto-detect React version so plugin:react/recommended doesn't warn
    react: { version: 'detect' },
  },
  rules: {
    // React 18 JSX transform — no need to import React in every file
    'react/react-in-jsx-scope': 'off',
    // TypeScript types serve as prop validation
    'react/prop-types': 'off',
    // Warn on default exports from modules that React Fast Refresh can't handle
    'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    // Enforce consistent import ordering
    'import/order': [
      'warn',
      {
        groups: ['builtin', 'external', 'internal', 'parent', 'sibling', 'index'],
        'newlines-between': 'always',
        alphabetize: { order: 'asc', caseInsensitive: true },
      },
    ],
  },
};

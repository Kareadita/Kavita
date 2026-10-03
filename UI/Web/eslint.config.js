// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    ignores: [
      'dist/**',
      '.angular/**',
      'coverage/**',
      'projects/**',
      'src/assets/**',
      '*.js',
    ],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/component-selector': [
        'error',
        {prefix: 'app', style: 'kebab-case', type: 'element'},
      ],
      '@angular-eslint/directive-selector': [
        'error',
        {prefix: 'app', style: 'camelCase', type: 'attribute'},
      ],
      '@typescript-eslint/no-unused-vars': 'off',
      'no-prototype-builtins': 'off',
      '@typescript-eslint/no-explicit-any': 'warn',
      'no-case-declarations': 'off',
      '@typescript-eslint/no-non-null-asserted-optional-chain': "warn"
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended],
    rules: {},
  },
);

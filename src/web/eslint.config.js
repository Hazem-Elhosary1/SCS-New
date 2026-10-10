// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/**
 * Layout must follow the reading direction (SH-002), so templates may use only logical
 * utilities (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`, `text-start`, `text-end`). The
 * physical ones would be wrong in either Arabic or English.
 */
const PHYSICAL_UTILITY =
  /^(?:[a-z0-9-]+:)*-?(?:(?:ml|mr|pl|pr|scroll-ml|scroll-mr|scroll-pl|scroll-pr|left|right)-.+|text-(?:left|right)|float-(?:left|right)|clear-(?:left|right)|(?:rounded|border)-(?:l|r|tl|tr|bl|br)(?:-.+)?)$/;

const logicalProperties = {
  meta: {
    type: 'problem',
    docs: { description: 'Use logical (start/end) utilities instead of physical left/right ones' },
    schema: [],
    messages: {
      physical:
        '"{{name}}" is a physical left/right utility. Use the logical one (ms-, me-, ps-, pe-, start-, end-, text-start, text-end) so the layout follows the language direction.',
    },
  },
  create(context) {
    const services = context.sourceCode.parserServices;

    function check(node, text) {
      if (typeof text !== 'string') return;
      for (const name of text.split(/\s+/)) {
        if (PHYSICAL_UTILITY.test(name)) {
          context.report({
            loc: services.convertNodeSourceSpanToLoc(node.sourceSpan),
            messageId: 'physical',
            data: { name },
          });
        }
      }
    }

    return {
      'TextAttribute[name="class"]'(node) {
        check(node, node.value);
      },
      // [class]="'...'", [ngClass]="{ 'ml-2': x }" and [class.ml-2]="x".
      'BoundAttribute LiteralPrimitive'(node) {
        check(node, node.value);
      },
      'BoundAttribute LiteralMap'(node) {
        for (const key of node.keys ?? []) check(node, key.key);
      },
      'BoundAttribute[keySpan.details=/^class\\./]'(node) {
        check(node, node.name);
      },
    };
  },
};

module.exports = defineConfig([
  {
    ignores: ['dist/**', '.angular/**', 'coverage/**', 'playwright-report/**', 'test-results/**'],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    plugins: { scs: { rules: { 'logical-properties': logicalProperties } } },
    rules: { 'scs/logical-properties': 'error' },
  },
]);

// @ts-check
const path = require('node:path');

/**
 * A feature folder (src/app/features/<name>/) may import only its own files, core/,
 * shared/ and the generated api/. Shared code moves to core/ or shared/, so parallel
 * slices never depend on each other's screens.
 * @type {import('eslint').Rule.RuleModule}
 */
const noCrossFeatureImports = {
  meta: {
    type: 'problem',
    docs: { description: 'A feature imports only core, shared, the generated api and itself.' },
    messages: {
      crossFeature: 'Feature "{{from}}" may not import from "{{to}}"; move shared code to core/ or shared/.',
    },
    schema: [],
  },
  create(context) {
    const file = context.filename.split(path.sep).join('/');
    const own = file.match(/^(.*\/src\/app)\/features\/([^/]+)\//);
    if (!own) return {};
    const [, appRoot, feature] = own;
    const allowed = ['core', 'shared', 'api', `features/${feature}`];

    /** @param {import('estree').Node & { source?: unknown }} node */
    const check = (node) => {
      const source = /** @type {{ value?: unknown } | undefined} */ (node.source);
      if (!source || typeof source.value !== 'string' || !source.value.startsWith('.')) return;
      const target = path.posix.normalize(path.posix.join(path.posix.dirname(file), source.value));
      if (!target.startsWith(`${appRoot}/`)) return;
      const inside = target.slice(appRoot.length + 1);
      if (allowed.some((root) => inside === root || inside.startsWith(`${root}/`))) return;
      context.report({ node, messageId: 'crossFeature', data: { from: feature, to: inside } });
    };

    return { ImportDeclaration: check, ImportExpression: check, ExportNamedDeclaration: check, ExportAllDeclaration: check };
  },
};

module.exports = { rules: { 'no-cross-feature-imports': noCrossFeatureImports } };

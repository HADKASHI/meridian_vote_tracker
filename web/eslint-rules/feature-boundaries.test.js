// Run by npm test: node --test "eslint-rules/*.test.js"
const { RuleTester } = require('eslint');
const { test } = require('node:test');
const plugin = require('./feature-boundaries');

const tester = new RuleTester({ languageOptions: { ecmaVersion: 2022, sourceType: 'module' } });
const file = '/repo/web/src/app/features/imports/import-page.ts';

test('a feature imports only core, shared, the api and itself', () => {
  tester.run('no-cross-feature-imports', plugin.rules['no-cross-feature-imports'], {
    valid: [
      { filename: file, code: "import { CurrentUser } from '../../core/identity/current-user';" },
      { filename: file, code: "import { pollWhile } from '../../shared/http/poll';" },
      { filename: file, code: "import { ImportDto } from '../../api/model';" },
      { filename: file, code: "import { Report } from './report/report';" },
      { filename: file, code: "import { Component } from '@angular/core';" },
      { filename: '/repo/web/src/app/app.routes.ts', code: "import('./features/review/review.routes');" },
    ],
    invalid: [
      { filename: file, code: "import { ReviewPage } from '../review/review-page';", errors: [{ messageId: 'crossFeature' }] },
      { filename: file, code: "const m = import('../votes/vote-page');", errors: [{ messageId: 'crossFeature' }] },
      { filename: file, code: "import { routes } from '../../app.routes';", errors: [{ messageId: 'crossFeature' }] },
    ],
  });
});

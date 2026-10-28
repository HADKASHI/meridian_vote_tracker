import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Imports', loadComponent: () => import('./imports-page').then((m) => m.ImportsPage) },
  { path: ':importId', title: 'Import report', loadComponent: () => import('./import-page').then((m) => m.ImportPage) },
] satisfies Routes;

import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Jobs', loadComponent: () => import('./jobs-page').then((m) => m.JobsPage) },
] satisfies Routes;

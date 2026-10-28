import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Review', loadComponent: () => import('./review-page').then((m) => m.ReviewPage) },
  { path: ':conflictId', title: 'Conflict', loadComponent: () => import('./conflict-page').then((m) => m.ConflictPage) },
] satisfies Routes;

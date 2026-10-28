import { Routes } from '@angular/router';
import { ReviewInbox } from './review-inbox';

export default [
  {
    path: '',
    title: 'Review',
    providers: [ReviewInbox],
    loadComponent: () => import('./review-page').then((m) => m.ReviewPage),
    children: [
      { path: ':conflictId', title: 'Conflict', loadComponent: () => import('./conflict-page').then((m) => m.ConflictPage) },
    ],
  },
] satisfies Routes;

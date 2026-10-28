import { Routes } from '@angular/router';

/** One lazy route file per feature. Filters, tabs and pages live in query params; ids in paths are internal ids. */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'review' },
  { path: 'imports', loadChildren: () => import('./features/imports/imports.routes') },
  { path: 'review', loadChildren: () => import('./features/review/review.routes') },
  { path: 'votes', loadChildren: () => import('./features/votes/votes.routes') },
  { path: 'positions', loadChildren: () => import('./features/positions/positions.routes') },
  { path: 'snapshots', loadChildren: () => import('./features/snapshots/snapshots.routes') },
  { path: 'publications', loadChildren: () => import('./features/snapshots/publications.routes') },
  { path: 'jobs', loadChildren: () => import('./features/jobs/jobs.routes') },
  { path: '**', title: 'Not found', loadComponent: () => import('./core/shell/not-found-page').then((m) => m.NotFoundPage) },
];

import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Snapshots', loadComponent: () => import('./snapshots-page').then((m) => m.SnapshotsPage) },
  { path: 'new', title: 'New snapshot', loadComponent: () => import('./new-snapshot-page').then((m) => m.NewSnapshotPage) },
  { path: 'compare', title: 'Compare snapshots', loadComponent: () => import('./compare-page').then((m) => m.ComparePage) },
  { path: ':snapshotId', title: 'Snapshot', loadComponent: () => import('./snapshot-page').then((m) => m.SnapshotPage) },
] satisfies Routes;

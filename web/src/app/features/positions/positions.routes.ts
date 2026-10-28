import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Positions', loadComponent: () => import('./positions-page').then((m) => m.PositionsPage) },
] satisfies Routes;

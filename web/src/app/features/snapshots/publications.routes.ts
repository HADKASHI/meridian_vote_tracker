import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Publications', loadComponent: () => import('./publications-page').then((m) => m.PublicationsPage) },
] satisfies Routes;

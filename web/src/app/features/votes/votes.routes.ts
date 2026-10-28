import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Votes', loadComponent: () => import('./votes-page').then((m) => m.VotesPage) },
  { path: ':voteId', title: 'Vote', loadComponent: () => import('./vote-page').then((m) => m.VotePage) },
] satisfies Routes;

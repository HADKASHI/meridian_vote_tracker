import { Routes } from '@angular/router';
import { ExtractionsInbox } from './extractions-inbox';

export default [
  {
    path: '',
    title: 'Extractions',
    providers: [ExtractionsInbox],
    loadComponent: () => import('./extractions-page').then((m) => m.ExtractionsPage),
    children: [
      { path: ':extractionId', title: 'Extraction', loadComponent: () => import('./extraction-page').then((m) => m.ExtractionPage) },
    ],
  },
] satisfies Routes;

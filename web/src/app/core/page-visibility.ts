import { DOCUMENT, inject, Injectable, signal } from '@angular/core';

/** Whether the tab is visible; polling pauses while it is hidden. */
@Injectable({ providedIn: 'root' })
export class PageVisibility {
  private readonly document = inject(DOCUMENT);
  readonly visible = signal(this.document.visibilityState !== 'hidden');

  constructor() {
    this.document.addEventListener('visibilitychange', () =>
      this.visible.set(this.document.visibilityState !== 'hidden'),
    );
  }
}

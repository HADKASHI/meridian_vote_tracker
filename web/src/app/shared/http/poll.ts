import { effect, inject, ResourceStatus, Signal } from '@angular/core';
import { PageVisibility } from '../../core/page-visibility';

export interface Reloadable {
  status: Signal<ResourceStatus>;
  reload(): boolean;
}

/**
 * Reloads a resource every `ms` while `active()` is true and the tab is visible. Each
 * reload is scheduled after the previous response, so calls never overlap on a slow
 * API. Call in an injection context; it stops with the component.
 */
export function pollWhile(resource: Reloadable, ms: number | (() => number), active: () => boolean): void {
  const visible = inject(PageVisibility).visible;
  effect((onCleanup) => {
    const status = resource.status();
    if ((status !== 'resolved' && status !== 'error') || !active() || !visible()) return;
    const timer = setTimeout(() => resource.reload(), typeof ms === 'function' ? ms() : ms);
    onCleanup(() => clearTimeout(timer));
  });
}

import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Params, RouterLink } from '@angular/router';

/** Where a detail page's breadcrumb leads: the list the page came from. */
export interface BackLink {
  label: string;
  link: string;
  queryParams?: Params;
}

/**
 * Every page opens with this: the title, on detail pages a breadcrumb back to its list,
 * a lead line (`[pageLead]`) and the page's main actions (`[pageActions]`).
 */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (back(); as b) {
      <nav aria-label="Breadcrumb" class="crumbs">
        <a [routerLink]="b.link" [queryParams]="b.queryParams">{{ b.label }}</a>
        <span aria-hidden="true">/</span>
        <span aria-current="page">{{ heading() }}</span>
      </nav>
    }
    <div class="title-row">
      <h1>{{ heading() }}</h1>
      <div class="actions"><ng-content select="[pageActions]" /></div>
    </div>
    <div class="lead"><ng-content select="[pageLead]" /></div>
    <ng-content />
  `,
  styles: `
    :host {
      display: block;
      margin-block-end: 20px;
    }
    .crumbs {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
      margin-block-end: 4px;
    }
    .crumbs a {
      color: var(--mat-sys-primary);
    }
    .crumbs [aria-current] {
      overflow-wrap: anywhere;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px 16px;
    }
    h1 {
      font: var(--mat-sys-headline-small);
      margin: 0;
      overflow-wrap: anywhere;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .actions:empty {
      display: none;
    }
    .lead {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
    .lead ::ng-deep p {
      margin-block: 4px 0;
    }
  `,
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly back = input<BackLink | undefined>();
}

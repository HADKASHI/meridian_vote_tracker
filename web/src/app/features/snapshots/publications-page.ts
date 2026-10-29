import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { listPublicationsResource } from '../../api/snapshots/snapshots.resource';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';

/** SNP-05: each reporting quarter's official index, and every publication it replaced. */
@Component({
  selector: 'app-publications-page',
  imports: [DatePipe, RouterLink, PageHeader, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Publications">
      <p pageLead>Each reporting quarter's official index, and every publication it replaced with the reason.</p>
    </app-page-header>
    @if (quarters.hasValue() && quarters.value(); as list) {
      @for (quarter of list; track quarter.quarter) {
        <section class="surface-card quarter" [attr.data-quarter]="quarter.quarter">
          <h2>{{ quarter.quarter }}</h2>
          <p class="current">
            Official index:
            <a [routerLink]="['/snapshots', quarter.current.snapshotId]">{{ quarter.current.snapshotName }}</a>
            · published by {{ quarter.current.publishedBy }} at {{ quarter.current.publishedAt | date: 'yyyy-MM-dd HH:mm' }}
          </p>
          @if (quarter.chain.length > 1) {
            <ol class="chain">
              @for (p of quarter.chain; track p.id) {
                <li [class.superseded]="p.supersededByPublicationId !== null">
                  <a [routerLink]="['/snapshots', p.snapshotId]">{{ p.snapshotName }}</a>, by {{ p.publishedBy }} at
                  {{ p.publishedAt | date: 'yyyy-MM-dd HH:mm' }}
                  @if (p.reason) {
                    · reason: {{ p.reason }}
                  }
                  @if (p.supersededByPublicationId; as next) {
                    · superseded by {{ snapshotNameOf(quarter.chain, next) }}, reason: {{ reasonOf(quarter.chain, next) }}
                  }
                </li>
              }
            </ol>
          }
        </section>
      } @empty {
        <app-load-state kind="empty" message="No snapshot has been published yet." />
      }
    } @else if (quarters.error()) {
      <app-load-state kind="error" message="The publications could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Loading publications" />
    }
  `,
  styles: `
    .quarter {
      max-width: 860px;
      overflow-wrap: anywhere;
    }
    h2 {
      font: var(--mat-sys-title-large);
      margin-block: 0 8px;
    }
    .current {
      margin-block: 0 8px;
    }
    .chain {
      margin: 0;
      padding-inline-start: 20px;
    }
    .chain li {
      margin-block: 4px;
    }
    .superseded {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class PublicationsPage {
  protected readonly quarters = listPublicationsResource();

  protected snapshotNameOf(chain: { id: number; snapshotName: string }[], id: number): string {
    return chain.find((p) => p.id === id)?.snapshotName ?? `publication #${id}`;
  }

  protected reasonOf(chain: { id: number; reason: string | null }[], id: number): string {
    return chain.find((p) => p.id === id)?.reason ?? '';
  }
}

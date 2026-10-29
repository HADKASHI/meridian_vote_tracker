import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { listPublicationsResource } from '../../api/snapshots/snapshots.resource';

/** SNP-05: each reporting quarter's official index, and every publication it replaced. */
@Component({
  selector: 'app-publications-page',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Publications</h1>
    @if (quarters.hasValue() && quarters.value(); as list) {
      @for (quarter of list; track quarter.quarter) {
        <section class="quarter" [attr.data-quarter]="quarter.quarter">
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
        <p>No snapshot has been published yet.</p>
      }
    }
  `,
  styles: `
    .quarter {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      padding: 8px 16px;
      margin-block: 12px;
      max-width: 860px;
    }
    .superseded {
      opacity: 0.75;
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

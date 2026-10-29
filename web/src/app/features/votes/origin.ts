import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OriginDto } from '../../api/model';

/** Where an approved value came from: the file row that brought it in, or the decision that approved it. */
@Component({
  selector: 'app-origin',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let o = origin();
    @if (o.kind === 'import_row') {
      <a [routerLink]="['/imports', o.importId]">{{ o.fileName }}</a>, line {{ o.lineNo }}, uploaded by
      {{ o.uploadedBy }} {{ o.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}
    } @else {
      <a [routerLink]="['/review', o.conflictId]">Decision on conflict #{{ o.conflictId }}</a> by {{ o.decidedBy }}
      {{ o.decidedAt | date: 'yyyy-MM-dd HH:mm' }}
    }
  `,
})
export class Origin {
  readonly origin = input.required<OriginDto>();
}

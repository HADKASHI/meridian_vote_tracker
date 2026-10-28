import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicationDto, QuarterPublicationsDto } from '../../api/model';
import { PublicationsPage } from './publications-page';

const first: PublicationDto = {
  id: 5, snapshotId: 3, snapshotName: 'First cut', quarter: '2024-Q1', publishedBy: 'lena',
  publishedAt: '2024-04-02T09:00:00Z', reason: null, supersedesPublicationId: null, supersededByPublicationId: 8,
};
const replacement: PublicationDto = {
  ...first, id: 8, snapshotId: 4, snapshotName: 'Corrected', reason: 'Rev2 corrections accepted.',
  supersedesPublicationId: 5, supersededByPublicationId: null,
};

describe('PublicationsPage', () => {
  it('shows the official index and keeps the superseded one visible with its replacement and reason', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PublicationsPage);
    TestBed.tick();
    http.expectOne('/api/publications').flush([
      { quarter: '2024-Q1', current: replacement, chain: [first, replacement] },
    ] satisfies QuarterPublicationsDto[]);
    await fixture.whenStable();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.current')?.textContent).toContain('Corrected');
    expect(page.querySelector('.superseded')?.textContent?.replace(/\s+/g, ' ')).toContain(
      'superseded by Corrected, reason: Rev2 corrections accepted.',
    );
  });
});

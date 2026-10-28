import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatButtonToggleHarness } from '@angular/material/button-toggle/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { PositionDto } from '../../api/model';
import { CurrentUser } from '../../core/identity/current-user';
import { PositionEditor } from './position-editor';

const seen: PositionDto = { revisionId: 11, stance: 'yes', weight: 2, setBy: 'lena', setAt: '2024-03-01T14:02:00Z' };

describe('PositionEditor', () => {
  let http: HttpTestingController;

  function setUp(isAdmin: boolean) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CurrentUser, useValue: { isAdmin: signal(isAdmin) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PositionEditor);
    fixture.componentRef.setInput('voteId', 7);
    fixture.componentRef.setInput('position', seen);
    fixture.detectChanges();
    return fixture;
  }

  function chooseWeight(element: HTMLElement, weight: string) {
    const select = element.querySelector<HTMLSelectElement>('[data-testid="weight"]')!;
    select.value = weight;
    select.dispatchEvent(new Event('change'));
  }

  it('saves the new stance and weight with the revision it showed as the version', async () => {
    const fixture = setUp(true);
    const saved: PositionDto[] = [];
    fixture.componentInstance.saved.subscribe((p) => saved.push(p));
    const loader = TestbedHarnessEnvironment.loader(fixture);

    await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'No' }))).check();
    chooseWeight(fixture.nativeElement, '5');
    fixture.detectChanges();
    fixture.nativeElement.querySelector('[data-testid="save"]').click();

    const request = http.expectOne('/api/votes/7/position');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ stance: 'no', weight: 5, seenRevisionId: 11 });
    const position: PositionDto = { ...seen, revisionId: 12, stance: 'no', weight: 5 };
    request.flush(position);
    expect(saved).toEqual([position]);
  });

  it('keeps Save disabled until something changed', () => {
    const fixture = setUp(true);

    expect(fixture.nativeElement.querySelector('[data-testid="save"]').disabled).toBe(true);
  });

  it('shows an analyst the controls disabled with "Admin only"', async () => {
    const fixture = setUp(false);
    const loader = TestbedHarnessEnvironment.loader(fixture);

    expect(fixture.nativeElement.textContent).toContain('Admin only');
    expect(fixture.nativeElement.querySelector('[data-testid="weight"]').disabled).toBe(true);
    expect(await (await loader.getHarness(MatButtonToggleHarness.with({ text: 'No' }))).isDisabled()).toBe(true);
    chooseWeight(fixture.nativeElement, '5');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="save"]').disabled).toBe(true);
  });
});

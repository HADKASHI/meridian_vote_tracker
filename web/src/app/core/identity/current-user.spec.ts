import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UserDto } from '../../api/model';
import { CurrentUser, LAST_USER_KEY, TAB_USER_KEY } from './current-user';

const users: UserDto[] = [
  { id: 'dana', displayName: 'Dana', role: 'analyst' },
  { id: 'lena', displayName: 'Lena', role: 'admin' },
  { id: 'omar', displayName: 'Omar', role: 'analyst' },
];

async function start(): Promise<CurrentUser> {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  const current = TestBed.inject(CurrentUser);
  TestBed.tick();
  TestBed.inject(HttpTestingController).expectOne('/api/users').flush(users);
  await TestBed.inject(ApplicationRef).whenStable();
  return current;
}

describe('CurrentUser', () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('starts as the first seeded user when nothing was chosen', async () => {
    const current = await start();

    expect(current.id()).toBe('dana');
  });

  it('keeps the tab its own user across reloads', async () => {
    sessionStorage.setItem(TAB_USER_KEY, 'omar');
    localStorage.setItem(LAST_USER_KEY, 'lena');

    expect((await start()).id()).toBe('omar');
  });

  it('starts a new tab as the user last chosen anywhere', async () => {
    localStorage.setItem(LAST_USER_KEY, 'lena');

    const current = await start();

    expect(current.id()).toBe('lena');
    expect(current.isAdmin()).toBe(true);
  });

  it('remembers a switch for this tab and for new tabs', async () => {
    const current = await start();

    current.switchTo('omar');

    expect(current.id()).toBe('omar');
    expect(sessionStorage.getItem(TAB_USER_KEY)).toBe('omar');
    expect(localStorage.getItem(LAST_USER_KEY)).toBe('omar');
  });

  it('forgets the chosen user and asks for another when the API rejects it', async () => {
    sessionStorage.setItem(TAB_USER_KEY, 'omar');
    const current = await start();

    current.rejected();

    expect(current.switcherRequested()).toBe(true);
    expect(sessionStorage.getItem(TAB_USER_KEY)).toBeNull();
    expect(current.id()).toBe('dana');
  });
});

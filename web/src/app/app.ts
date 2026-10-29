import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { UserSwitcher } from './core/identity/user-switcher';
import { JobNotices } from './core/status/job-notices';
import { JobsIndicator } from './core/status/jobs-indicator';
import { ShellStatus } from './core/status/shell-status';

/** The shell: navigation, the Review badge, the jobs indicator and the user switcher. */
@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet, MatToolbarModule, MatButtonModule, MatBadgeModule,
    MatMenuModule, UserSwitcher, JobsIndicator,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly shell = inject(ShellStatus);
  protected readonly navLinks = [
    { path: '/review', label: 'Review' },
    { path: '/imports', label: 'Imports' },
    { path: '/votes', label: 'Votes' },
    { path: '/positions', label: 'Positions' },
    { path: '/snapshots', label: 'Snapshots' },
    { path: '/publications', label: 'Publications' },
    { path: '/jobs', label: 'Jobs' },
  ];
  protected readonly reviewLabel = computed(() => {
    const pending = this.shell.pendingConflicts();
    return pending > 0 ? `Review, ${pending} pending` : 'Review';
  });

  constructor() {
    inject(JobNotices);
  }
}

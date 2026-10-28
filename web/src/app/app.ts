import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { UserSwitcher } from './core/identity/user-switcher';
import { JobNotices } from './core/status/job-notices';
import { JobsIndicator } from './core/status/jobs-indicator';
import { ShellStatus } from './core/status/shell-status';

/** The shell: navigation, the Review badge, the jobs indicator and the user switcher. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatBadgeModule, UserSwitcher, JobsIndicator],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly shell = inject(ShellStatus);

  constructor() {
    inject(JobNotices);
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../core/auth.service';
import { friendlyProblem } from '../core/api-messages';

interface DemoUser {
  userName: string;
  displayName: string;
  hint: string;
}

/**
 * Development sign-in screen. The production build replaces this flow with the
 * Azure AD B2C redirect via MSAL — the Auth service contract stays identical
 * (`token` in storage), so only this component is swapped out (docs/security.md).
 */
@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-wrap">
      <div class="card login-card">
        <div class="brand"><span class="logo">₣</span> FinTrack</div>
        <p class="muted">Sign in to your internet banking portal.</p>

        <div class="demo-grid">
          @for (u of users; track u.userName) {
            <button class="btn ghost demo-user" (click)="signIn(u)" [disabled]="busy()">
              <span>
                <strong>{{ u.displayName }}</strong>
                <div class="muted demo-hint">{{ u.hint }}</div>
              </span>
              <span class="badge checking">{{ u.userName }}</span>
            </button>
          }
        </div>

        @if (error(); as msg) {
          <div class="global-error mt">{{ msg }}</div>
        }

        <p class="muted fine-print">
          Selecting a profile exchanges the demo user id for a signed JWT — the same
          token contract the API enforces with role policies in production.
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly users: DemoUser[] = [
    { userName: 'admin', displayName: 'Priya Shah', hint: 'Admin — full overview, account lifecycle' },
    { userName: 'teller', displayName: 'Marco Diaz', hint: 'Teller — back-office operations' },
    { userName: 'amira', displayName: 'Amira Hassan', hint: 'Retail customer' },
    { userName: 'tarek', displayName: 'Tarek Fahmy', hint: 'Retail customer' },
  ];

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async signIn(user: DemoUser): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(user.userName);
      // A real navigation keeps the guard as the single source of truth for access.
      await this.router.navigateByUrl('/dashboard');
    } catch (err) {
      this.error.set(friendlyProblem(err));
    } finally {
      this.busy.set(false);
    }
  }
}

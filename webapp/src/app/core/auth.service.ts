import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
}

/** Local development identity stub; production swaps MSAL/Azure AD B2C — see docs/security.md. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly user = signal<string | null>(this.readUser());
  readonly role = computed(() => this.firstRole());

  async login(userName: string): Promise<void> {
    const res = await firstValueFrom(this.http.post<TokenResponse>('/api/auth/token', { userName }));
    localStorage.setItem('fintrack.token', res.accessToken);
    localStorage.setItem('fintrack.user', userName);
    this.user.set(userName);
  }

  logout(): void {
    localStorage.removeItem('fintrack.token');
    localStorage.removeItem('fintrack.user');
    this.user.set(null);
  }

  get token(): string | null {
    return localStorage.getItem('fintrack.token');
  }

  private readUser(): string | null {
    return localStorage.getItem('fintrack.user');
  }

  private firstRole(): string {
    const payload = this.decode(this.token);
    const roles = payload?.['roles'];
    return Array.isArray(roles) ? roles.join(', ') : (roles as string) ?? '';
  }

  private decode(token: string | null): Record<string, unknown> | null {
    if (!token) return null;
    try {
      return JSON.parse(atob(token.split('.')[1]!.replaceAll('-', '+').replaceAll('_', '/')));
    } catch {
      return null;
    }
  }
}

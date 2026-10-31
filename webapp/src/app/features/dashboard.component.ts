import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AccountService } from '../core/account.service';
import { AuthService } from '../core/auth.service';
import type { AccountSummary, ApiError } from '../core/models';
import { money, signed } from '../core/format';

interface BalanceGroup {
  currency: string;
  total: number;
  count: number;
}

/** LedgerEntry decorated with owning-account details for the merged activity table. */
interface RecentRow {
  transactionId: string;
  reference: string;
  createdAt: string;
  amount: number;
  accountNumber: string;
  currency: string;
}

/**
 * Landing page: portfolio KPIs and the account book.
 * Staff get the full book; retail callers see their own accounts only.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Welcome back, {{ firstName() }}</h1>
      <p>
        @if (isRetail()) {
          Your accounts at a glance. Open a ledger for the full statement.
        } @else {
          Portfolio overview across the book. Staff can drill into any account.
        }
      </p>
    </header>

    @if (loading()) {
      <div class="loading">Loading accounts…</div>
    } @else if (error(); as msg) {
      <div class="global-error">{{ msg }}</div>
    } @else {
      <div class="kpi-grid">
        @for (group of balanceGroups(); track group.currency) {
          <div class="card kpi">
            <div class="label">Balance · {{ group.currency }}</div>
            <div class="value mono">{{ money(group.total, group.currency) }}</div>
            <div class="sub">{{ group.count }} account{{ group.count === 1 ? '' : 's' }}</div>
          </div>
        }
        <div class="card kpi">
          <div class="label">Open accounts</div>
          <div class="value">{{ activeCount() }}</div>
          <div class="sub">{{ closedCount() }} closed</div>
        </div>
      </div>

      <h2>Accounts</h2>
      @if (accounts().length === 0) {
        <div class="card empty">No accounts linked yet. Visit a branch to open one.</div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>Number</th>
                <th>Type</th>
                <th>Currency</th>
                <th>Status</th>
                <th style="text-align:right">Balance</th>
                <th>Opened</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (a of accounts(); track a.id) {
                <tr>
                  <td class="mono">{{ a.number }}</td>
                  <td><span class="badge {{ a.accountType.toLowerCase() }}">{{ a.accountType }}</span></td>
                  <td>{{ a.currency }}</td>
                  <td><span class="badge {{ a.status.toLowerCase() }}">{{ a.status }}</span></td>
                  <td class="mono" style="text-align:right">{{ money(a.balance, a.currency) }}</td>
                  <td>{{ a.openedAt | date: 'mediumDate' }}</td>
                  <td><a routerLink="/accounts/{{ a.id }}">Ledger →</a></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      <div class="card mt">
        <h2>Latest activity</h2>
        @if (recent().length === 0) {
          <div class="empty muted">No transactions recorded yet.</div>
        } @else {
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr><th>When</th><th>Reference</th><th>Account</th><th style="text-align:right">Amount</th></tr>
              </thead>
              <tbody>
                @for (t of recent(); track t.transactionId) {
                  <tr>
                    <td>{{ t.createdAt | date: 'short' }}</td>
                    <td class="mono">{{ t.reference }}</td>
                    <td class="mono">{{ t.accountNumber }}</td>
                    <td class="mono {{ t.amount >= 0 ? 'pos' : 'neg' }}" style="text-align:right">
                      {{ signed(t.amount, t.currency) }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    }
  `,
})
export class DashboardComponent {
  private readonly api = inject(AccountService);
  private readonly auth = inject(AuthService);

  protected readonly money = money;
  protected readonly signed = signed;

  readonly accounts = signal<AccountSummary[]>([]);
  readonly recent = signal<RecentRow[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly isRetail = computed(() => this.auth.role().includes('Customer'));
  readonly firstName = computed(() => (this.auth.user() ?? '').split(/[._-]/, 1)[0]);

  /** Balance totals grouped per currency — the API carries no global currency. */
  readonly balanceGroups = computed<BalanceGroup[]>(() => {
    const byCurrency = new Map<string, BalanceGroup>();
    for (const account of this.accounts()) {
      const group = byCurrency.get(account.currency) ?? { currency: account.currency, total: 0, count: 0 };
      group.total += account.balance;
      group.count += 1;
      byCurrency.set(account.currency, group);
    }
    return [...byCurrency.values()].sort((x, y) => y.total - x.total);
  });

  readonly activeCount = computed(() => this.accounts().filter(a => a.status === 'Active').length);
  readonly closedCount = computed(() => this.accounts().filter(a => a.status === 'Closed').length);

  constructor() {
    this.api.list().subscribe({
      next: accounts => {
        this.accounts.set(accounts);
        this.loading.set(false);
        this.loadRecentActivity(accounts);
      },
      error: (err: ApiError) => {
        this.error.set(err.detail ?? 'Could not load your accounts.');
        this.loading.set(false);
      },
    });
  }

  /**
   * Pulls a short ledger page of each of the most-recent accounts and merges the
   * newest rows. Individual failures degrade silently — the KPIs stay correct.
   */
  private async loadRecentActivity(accounts: AccountSummary[]): Promise<void> {
    if (accounts.length === 0) return;

    const snapshots = await Promise.allSettled(
      accounts.slice(0, 4).map(async account => {
        const page = await firstValueFrom(this.api.transactions(account.id, 1, 10));
        return page.items.map(entry => ({
          transactionId: entry.transactionId,
          reference: entry.reference,
          createdAt: entry.createdAt,
          amount: entry.amount,
          accountNumber: account.number,
          currency: account.currency,
        }));
      })
    );

    const merged = snapshots
      .flatMap(result => (result.status === 'fulfilled' ? result.value : []))
      .sort((x, y) => y.createdAt.localeCompare(x.createdAt));

    this.recent.set(merged.slice(0, 6));
  }
}

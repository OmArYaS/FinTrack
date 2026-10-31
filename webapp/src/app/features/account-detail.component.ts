import { ChangeDetectionStrategy, Component, Input, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { AccountService } from '../core/account.service';
import type { AccountSummary, ApiError, LedgerEntry, Paged } from '../core/models';
import { friendlyProblem } from '../core/api-messages';
import { money, shortDate, signed } from '../core/format';

const PAGE_SIZE = 20;

/**
 * Account statement: live summary plus the paged, newest-first ledger.
 * Access (owner vs staff vs stranger) is enforced server-side; 403/404 surfaces here.
 */
@Component({
  selector: 'app-account-detail',
  standalone: true,
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading">Loading account…</div>
    } @else if (fatal(); as msg) {
      <div class="global-error">
        {{ msg }}
        <a class="btn ghost" routerLink="/accounts">Back to accounts</a>
      </div>
    } @else if (account(); as a) {
      <header class="page-header">
        <h1 class="mono">{{ a.number }}</h1>
        <p>
          <span class="badge {{ a.status.toLowerCase() }}">{{ a.status }}</span>
          <span class="badge {{ a.accountType.toLowerCase() }}">{{ a.accountType }}</span>
          <span class="muted">Opened {{ a.openedAt | date: 'mediumDate' }} · {{ a.currency }}</span>
        </p>
      </header>

      <div class="card kpi">
        <div class="label">Available balance</div>
        <div class="value mono">{{ money(a.balance, a.currency) }}</div>
        <div class="sub">Ledger reflects settled entries only — nothing pending.</div>
      </div>

      <h2 class="mt">Ledger</h2>
      @if (paged().items.length === 0) {
        <div class="card empty">No transactions on this account yet.</div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>Posted</th>
                <th>Type</th>
                <th>Reference</th>
                <th>Description</th>
                <th style="text-align:right">Amount</th>
                <th style="text-align:right">Balance after</th>
              </tr>
            </thead>
            <tbody>
              @for (t of paged().items; track t.transactionId) {
                <tr>
                  <td>{{ shortDate(t.createdAt) }}</td>
                  <td><span class="badge {{ t.type.toLowerCase() }}">{{ t.type }}</span></td>
                  <td class="mono">{{ t.reference }}</td>
                  <td>{{ t.description ?? '—' }}</td>
                  <td class="mono {{ t.amount >= 0 ? 'pos' : 'neg' }}" style="text-align:right">
                    {{ signed(t.amount, a.currency) }}
                  </td>
                  <td class="mono" style="text-align:right">{{ money(t.balanceAfter, a.currency) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="btn-row pager">
          <button class="btn ghost" (click)="go(pageIndex() - 1)" [disabled]="!hasPrevious()">← Previous</button>
          <span class="muted">Page {{ pageIndex() }} of {{ totalPages() }} · {{ totalCount() }} entries</span>
          <button class="btn ghost" (click)="go(pageIndex() + 1)" [disabled]="!hasNext()">Next →</button>
        </div>
      }
    }
  `,
})
export class AccountDetailComponent {
  private readonly api = inject(AccountService);

  protected readonly money = money;
  protected readonly shortDate = shortDate;
  protected readonly signed = signed;

  /** Bound by withComponentInputBinding() from the /accounts/:id route. */
  @Input({ required: true }) id = '';

  readonly account = signal<AccountSummary | null>(null);
  readonly paged = signal<Paged<LedgerEntry>>({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0, totalPages: 0 });
  readonly loading = signal(true);
  readonly fatal = signal<string | null>(null);

  readonly pageIndex = signal(1);
  readonly hasNext = computed(() => this.pageIndex() < this.paged().totalPages);
  readonly hasPrevious = computed(() => this.pageIndex() > 1);
  readonly totalPages = computed(() => this.paged().totalPages);
  readonly totalCount = computed(() => this.paged().totalCount);

  /** Runs after @Input bindings are set — the upstream route param is in place by now. */
  ngOnInit(): void {
    this.fetch(1);
  }

  go(page: number): void {
    const clamped = Math.max(1, page);
    if (clamped === this.pageIndex()) return;
    this.fetch(clamped);
  }

  /** Loads summary + ledger page concurrently; either failure passes to the error view. */
  private fetch(page: number): void {
    this.api.get(this.id).subscribe({
      next: summary => this.account.set(summary),
      error: (err: ApiError) => {
        this.fatal.set(friendlyProblem(err));
        this.loading.set(false);
      },
    });

    this.api.transactions(this.id, page, PAGE_SIZE).subscribe({
      next: result => {
        this.pageIndex.set(result.page);
        this.paged.set(result);
        this.loading.set(false);
        this.fatal.set(null);
      },
      error: (err: ApiError) => {
        this.fatal.set(friendlyProblem(err));
        this.loading.set(false);
      },
    });
  }
}

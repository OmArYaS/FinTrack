import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { AccountService } from '../core/account.service';
import { AuthService } from '../core/auth.service';
import type { AccountSummary, ApiError, Customer } from '../core/models';
import { friendlyProblem } from '../core/api-messages';
import { money, shortDay } from '../core/format';

const ACCOUNT_TYPES = ['Checking', 'Savings', 'FixedDeposit'];
const CURRENCIES = ['EGP', 'USD', 'EUR'];

/**
 * Account book. Retail callers see their own accounts; staff get the full book
 * with a customer filter plus account lifecycle operations (open, close).
 */
@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Accounts</h1>
      @if (isStaff()) {
        <p>The full book. Filter by customer, or run account lifecycle operations.</p>
      } @else {
        <p>Your accounts. Open a ledger for the full statement.</p>
      }
    </header>

    @if (error(); as msg) {
      <div class="global-error">
        {{ msg }}
        <button class="btn ghost" (click)="reload()">Retry</button>
      </div>
    }
    @if (notice(); as msg) {
      <div class="card notice">{{ msg }}</div>
    }

    @if (isStaff()) {
      <div class="card toolbar">
        <div>
          <label for="customer-filter">Customer filter</label>
          <select id="customer-filter" [value]="selectedCustomerId()" (change)="pickCustomer($event)">
            <option value="">All customers</option>
            @for (c of directory(); track c.id) {
              <option [value]="c.id">{{ c.fullName }} ({{ c.externalId }})</option>
            }
          </select>
        </div>
        <div class="spacer"></div>
        <button class="btn" (click)="toggleOpenForm()">{{ openForm() ? 'Cancel' : 'Open account' }}</button>
      </div>

      @if (openForm()) {
        <div class="card mt">
          <h2>Open account</h2>
          <form [formGroup]="form" (ngSubmit)="submitOpen()" class="form-grid">
            <label class="full">Customer
              <select formControlName="customerExternalId">
                <option [value]="''" disabled>Select the customer profile…</option>
                @for (c of directory(); track c.externalId) {
                  <option [value]="c.externalId">{{ c.fullName }} — {{ c.externalId }}</option>
                }
              </select>
              @if (attempted() && form.controls.customerExternalId.invalid) {
                <div class="field-error">Select the customer profile.</div>
              }
            </label>

            <label>Account type
              <select formControlName="accountType">
                @for (t of accountTypes; track t) {
                  <option [value]="t">{{ t }}</option>
                }
              </select>
            </label>

            <label>Currency
              <select formControlName="currency">
                @for (cur of currencyCodes; track cur) {
                  <option [value]="cur">{{ cur }}</option>
                }
              </select>
            </label>

            <div class="btn-row full">
              <button class="btn" type="submit" [disabled]="submitting()">
                {{ submitting() ? 'Opening…' : 'Open account' }}
              </button>
              <span class="muted">New accounts start zero-balanced with an opening ledger entry.</span>
            </div>
          </form>
        </div>
      }
    }

    @if (loading()) {
      <div class="loading mt">Loading accounts…</div>
    } @else if (visible().length === 0 && !error()) {
      <div class="card empty mt">No accounts to show.</div>
    } @else {
      <div class="table-wrap card mt">
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
            @for (a of visible(); track a.id) {
              <tr>
                <td class="mono">{{ a.number }}</td>
                <td><span class="badge {{ a.accountType.toLowerCase() }}">{{ a.accountType }}</span></td>
                <td>{{ a.currency }}</td>
                <td><span class="badge {{ a.status.toLowerCase() }}">{{ a.status }}</span></td>
                <td class="mono" style="text-align:right">{{ money(a.balance, a.currency) }}</td>
                <td>{{ shortDay(a.openedAt) }}</td>
                <td>
                  <div class="btn-row" style="margin-top: 0">
                    <a class="btn ghost" routerLink="/accounts/{{ a.id }}">Ledger</a>
                    @if (isStaff() && a.status !== 'Closed') {
                      <button class="btn danger" (click)="closeAccount(a)" [disabled]="busyId() === a.id">Close</button>
                    }
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class AccountsComponent {
  private readonly api = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly money = money;
  protected readonly shortDay = shortDay;

  /** Server-validated choices rendered into the open-account form. */
  readonly accountTypes = ACCOUNT_TYPES;
  readonly currencyCodes = CURRENCIES;

  readonly directory = signal<Customer[]>([]);
  readonly accounts = signal<AccountSummary[]>([]);
  readonly selectedCustomerId = signal('');
  readonly openForm = signal(false);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly attempted = signal(false);

  readonly isStaff = computed(() => !this.auth.role().includes('Customer'));

  readonly form = this.fb.group({
    customerExternalId: ['', Validators.required],
    accountType: ['Checking'],
    currency: ['EGP'],
  });

  readonly visible = computed(() => {
    const filter = this.selectedCustomerId();
    return filter ? this.accounts().filter(a => a.customerId === filter) : this.accounts();
  });

  constructor() {
    this.reload();
    void this.loadDirectory();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.list().subscribe({
      next: accounts => { this.accounts.set(accounts); this.loading.set(false); },
      error: (err: ApiError) => { this.error.set(friendlyProblem(err)); this.loading.set(false); },
    });
  }

  private async loadDirectory(): Promise<void> {
    if (!this.isStaff()) return;
    try {
      this.directory.set(await firstValueFrom(this.api.customers()));
    } catch {
      this.directory.set([]); // filter without directory degrades to the full book
    }
  }

  async closeAccount(account: AccountSummary): Promise<void> {
    this.busyId.set(account.id);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.close(account.id));
      this.reload();
    } catch (err) {
      this.error.set(friendlyProblem(err));
    } finally {
      this.busyId.set(null);
    }
  }

  toggleOpenForm(): void {
    this.openForm.update(open => !open);
    this.error.set(null);
    this.notice.set(null);
    this.attempted.set(false);
    if (this.openForm() && this.directory().length > 0) {
      const first = this.directory()[0]!; // directory is sorted by full name
      this.form.patchValue({ customerExternalId: this.form.value.customerExternalId || first.externalId });
    }
  }

  async submitOpen(): Promise<void> {
    this.attempted.set(true);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const opened = await firstValueFrom(this.api.open(this.form.getRawValue()));
      this.form.reset({ customerExternalId: '', accountType: 'Checking', currency: 'EGP' });
      this.openForm.set(false);
      this.reload();
      this.notice.set(`Account ${opened.number} opened and ready for its first transaction.`);
    } catch (err) {
      this.error.set(friendlyProblem(err));
    } finally {
      this.submitting.set(false);
    }
  }
}

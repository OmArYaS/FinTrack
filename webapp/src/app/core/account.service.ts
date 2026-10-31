import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import type { AccountSummary, ApiProblem, Customer, Paged, LedgerEntry } from './models';
import { TransferService } from './transfer.service';

/** Extracts the stable business code from a ProblemDetails response. */
export function problemOf(error: HttpErrorResponse): ApiProblem {
  return (typeof error.error === 'object' && error.error) ? error.error as ApiProblem : {};
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api';

  /** Customer-scoped listing; staff may list all or browse any customer. */
  list(customerId?: string): Observable<AccountSummary[]> {
    const qs = customerId ? `?customerId=${customerId}` : '';
    return this.http.get<AccountSummary[]>(`${this.baseUrl}/accounts${qs}`)
      .pipe(catchError(this.toProblem));
  }

  get(id: string): Observable<AccountSummary> {
    return this.http.get<AccountSummary>(`${this.baseUrl}/accounts/${id}`)
      .pipe(catchError(this.toProblem));
  }

  transactions(id: string, page = 1, pageSize = 25): Observable<Paged<LedgerEntry>> {
    return this.http.get<Paged<LedgerEntry>>(`${this.baseUrl}/accounts/${id}/transactions?page=${page}&pageSize=${pageSize}`)
      .pipe(catchError(this.toProblem));
  }

  open(request: { customerExternalId: string; accountType: string; currency: string }): Observable<AccountSummary> {
    return this.http.post<AccountSummary>(`${this.baseUrl}/accounts`, request)
      .pipe(catchError(this.toProblem));
  }

  close(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/accounts/${id}/close`, {})
      .pipe(catchError(this.toProblem));
  }

  customers(): Observable<Customer[]> {
    return this.http.get<Customer[]>(`${this.baseUrl}/customers`)
      .pipe(catchError(this.toProblem));
  }

  /** Shared error extraction; TransferService reuses the same helper. */
  private toProblem = (error: HttpErrorResponse) =>
    throwError(() => ({ http: error.status, ...problemOf(error) }));
}

/** Convenience re-export so components import one module for both services. */
export { TransferService };

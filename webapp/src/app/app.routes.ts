import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { LayoutComponent } from './features/layout.component';
import { DashboardComponent } from './features/dashboard.component';
import { AccountsComponent } from './features/accounts.component';
import { AccountDetailComponent } from './features/account-detail.component';
import { TransfersComponent } from './features/transfers.component';

export const routes: Routes = [
  { path: '', component: LayoutComponent, canActivate: [authGuard], children: [
    { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    { path: 'dashboard', component: DashboardComponent },
    { path: 'accounts', component: AccountsComponent },
    { path: 'accounts/:id', component: AccountDetailComponent },
    { path: 'transfers', component: TransfersComponent },
  ]},
];

import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { Login } from './pages/login/login';
import { Shell } from './shell/shell';

export const routes: Routes = [
  { path: 'login', component: Login },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard) },
      { path: 'sales', loadComponent: () => import('./pages/sales/sales').then((m) => m.Sales) },
      { path: 'products', loadComponent: () => import('./pages/products/products').then((m) => m.Products) },
      { path: 'customers', loadComponent: () => import('./pages/customers/customers').then((m) => m.Customers) },
      { path: 'reports', loadComponent: () => import('./pages/reports/reports').then((m) => m.Reports) },
    ],
  },
  { path: '**', redirectTo: '' },
];

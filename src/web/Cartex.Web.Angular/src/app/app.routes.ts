import { Routes } from '@angular/router';
import { authGuard, landingGuard } from './core/auth.guard';
import { Login } from './pages/login/login';
import { Shell } from './shell/shell';

export const routes: Routes = [
  { path: 'login', component: Login },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [landingGuard], children: [] },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard) },
      { path: 'pos', loadComponent: () => import('./pages/pos/pos').then((m) => m.Pos) },
      { path: 'shift', loadComponent: () => import('./pages/shift/shift').then((m) => m.Shift) },
      { path: 'sales', loadComponent: () => import('./pages/sales/sales').then((m) => m.Sales) },
      { path: 'orders', loadComponent: () => import('./pages/orders/orders').then((m) => m.Orders) },
      { path: 'customers', loadComponent: () => import('./pages/customers/customers').then((m) => m.Customers) },
      { path: 'products', loadComponent: () => import('./pages/products/products').then((m) => m.Products) },
      { path: 'warehouse', loadComponent: () => import('./pages/warehouse/warehouse').then((m) => m.Warehouse) },
      { path: 'supplies', loadComponent: () => import('./pages/supplies/supplies').then((m) => m.Supplies) },
      { path: 'transfers', loadComponent: () => import('./pages/transfers/transfers').then((m) => m.Transfers) },
      { path: 'accounts', loadComponent: () => import('./pages/accounts/accounts').then((m) => m.Accounts) },
      { path: 'transactions', loadComponent: () => import('./pages/transactions/transactions').then((m) => m.Transactions) },
      { path: 'reports', loadComponent: () => import('./pages/reports/reports').then((m) => m.Reports) },
      {
        path: 'settings',
        loadComponent: () => import('./pages/settings/settings').then((m) => m.Settings),
        children: [
          { path: 'security', loadComponent: () => import('./pages/security/security').then((m) => m.Security) },
          { path: 'devices', loadComponent: () => import('./pages/devices/devices').then((m) => m.Devices) },
          { path: 'categories', loadComponent: () => import('./pages/categories/categories').then((m) => m.Categories) },
          { path: 'units', loadComponent: () => import('./pages/units/units').then((m) => m.Units) },
          { path: 'product-types', loadComponent: () => import('./pages/product-types/product-types').then((m) => m.ProductTypes) },
          { path: 'manufacturers', loadComponent: () => import('./pages/manufacturers/manufacturers').then((m) => m.Manufacturers) },
          { path: 'business', loadComponent: () => import('./pages/business/business').then((m) => m.BusinessSettings) },
          { path: 'branches', loadComponent: () => import('./pages/branches/branches').then((m) => m.Branches) },
          { path: 'warehouses', loadComponent: () => import('./pages/warehouses/warehouses').then((m) => m.Warehouses) },
          { path: 'suppliers', loadComponent: () => import('./pages/suppliers/suppliers').then((m) => m.Suppliers) },
          { path: 'users', loadComponent: () => import('./pages/users/users').then((m) => m.Users) },
          { path: 'roles', loadComponent: () => import('./pages/roles/roles').then((m) => m.Roles) },
          { path: 'loyalty', loadComponent: () => import('./pages/loyalty/loyalty').then((m) => m.Loyalty) },
          { path: 'expense-categories', loadComponent: () => import('./pages/expense-categories/expense-categories').then((m) => m.ExpenseCategories) },
          { path: 'rates', loadComponent: () => import('./pages/rates/rates').then((m) => m.Rates) },
          { path: 'audit', loadComponent: () => import('./pages/audit/audit').then((m) => m.Audit) },
          { path: 'license', loadComponent: () => import('./pages/license/license').then((m) => m.License) },
          { path: 'features', redirectTo: 'license' },
          { path: 'integrations', loadComponent: () => import('./pages/integrations/integrations').then((m) => m.Integrations) },
          { path: 'receipt-settings', loadComponent: () => import('./pages/receipt-settings/receipt-settings').then((m) => m.ReceiptSettings) },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

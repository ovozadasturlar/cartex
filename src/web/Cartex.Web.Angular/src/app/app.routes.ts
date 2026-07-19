import { Routes } from '@angular/router';
import { authGuard, landingGuard, permissionGuard } from './core/auth.guard';
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
      { path: 'customers/:id', loadComponent: () => import('./pages/customers/customer-profile').then((m) => m.CustomerProfile) },
      { path: 'products', loadComponent: () => import('./pages/products/products').then((m) => m.Products) },
      { path: 'warehouse', loadComponent: () => import('./pages/warehouse/warehouse').then((m) => m.Warehouse) },
      { path: 'supplies/new', loadComponent: () => import('./pages/supplies/supply-create').then((m) => m.SupplyCreate) },
      { path: 'supplies', loadComponent: () => import('./pages/supplies/supplies').then((m) => m.Supplies) },
      { path: 'transfers', loadComponent: () => import('./pages/transfers/transfers').then((m) => m.Transfers) },
      { path: 'accounts', loadComponent: () => import('./pages/accounts/accounts').then((m) => m.Accounts) },
      { path: 'transactions', loadComponent: () => import('./pages/transactions/transactions').then((m) => m.Transactions) },
      { path: 'reports', loadComponent: () => import('./pages/reports/reports').then((m) => m.Reports) },
      {
        path: 'settings',
        loadComponent: () => import('./pages/settings/settings').then((m) => m.Settings),
        canActivateChild: [permissionGuard],
        children: [
          { path: 'security', loadComponent: () => import('./pages/security/security').then((m) => m.Security) },
          { path: 'devices', data: { permission: 'devices.manage' }, loadComponent: () => import('./pages/devices/devices').then((m) => m.Devices) },
          { path: 'categories', data: { permission: 'categories.manage' }, loadComponent: () => import('./pages/categories/categories').then((m) => m.Categories) },
          { path: 'units', data: { permission: 'products.manage' }, loadComponent: () => import('./pages/units/units').then((m) => m.Units) },
          { path: 'product-types', data: { permission: 'products.manage' }, loadComponent: () => import('./pages/product-types/product-types').then((m) => m.ProductTypes) },
          { path: 'manufacturers', data: { permission: 'products.manage' }, loadComponent: () => import('./pages/manufacturers/manufacturers').then((m) => m.Manufacturers) },
          { path: 'business', data: { permission: 'business.manage' }, loadComponent: () => import('./pages/business/business').then((m) => m.BusinessSettings) },
          { path: 'branches', data: { permission: 'branches.manage' }, loadComponent: () => import('./pages/branches/branches').then((m) => m.Branches) },
          { path: 'warehouses', data: { permission: 'warehouses.manage' }, loadComponent: () => import('./pages/warehouses/warehouses').then((m) => m.Warehouses) },
          { path: 'suppliers', data: { permission: 'suppliers.manage' }, loadComponent: () => import('./pages/suppliers/suppliers').then((m) => m.Suppliers) },
          { path: 'users', data: { permission: 'users.view' }, loadComponent: () => import('./pages/users/users').then((m) => m.Users) },
          { path: 'roles', data: { permission: 'roles.view' }, loadComponent: () => import('./pages/roles/roles').then((m) => m.Roles) },
          { path: 'loyalty', data: { permission: 'loyalty.view' }, loadComponent: () => import('./pages/loyalty/loyalty').then((m) => m.Loyalty) },
          { path: 'expense-categories', data: { permission: 'business.manage' }, loadComponent: () => import('./pages/expense-categories/expense-categories').then((m) => m.ExpenseCategories) },
          { path: 'rates', data: { permission: 'rates.manage' }, loadComponent: () => import('./pages/rates/rates').then((m) => m.Rates) },
          { path: 'audit', data: { permission: 'audit.view' }, loadComponent: () => import('./pages/audit/audit').then((m) => m.Audit) },
          { path: 'license', data: { permission: 'features.manage' }, loadComponent: () => import('./pages/license/license').then((m) => m.License) },
          { path: 'features', redirectTo: 'license' },
          { path: 'integrations', data: { permission: 'settings.integrations' }, loadComponent: () => import('./pages/integrations/integrations').then((m) => m.Integrations) },
          { path: 'receipt-settings', data: { permission: 'settings.receipt' }, loadComponent: () => import('./pages/receipt-settings/receipt-settings').then((m) => m.ReceiptSettings) },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

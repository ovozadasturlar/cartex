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
      {
        path: 'dashboard',
        canActivate: [permissionGuard],
        data: { permission: 'reports.view' , feature: 'reports' },
        loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'pos',
        canActivate: [permissionGuard],
        data: { permission: 'sales.create|sales.checkout|sales.pick|sales.view', feature: 'ordering|store' },
        loadComponent: () => import('./pages/pos/pos').then((m) => m.Pos),
      },
      {
        path: 'shift',
        canActivate: [permissionGuard],
        data: { permission: 'shifts.view' },
        loadComponent: () => import('./pages/shift/shift').then((m) => m.Shift),
      },
      {
        path: 'sales',
        canActivate: [permissionGuard],
        data: { permission: 'sales.view' },
        loadComponent: () => import('./pages/sales/sales').then((m) => m.Sales),
      },
      {
        path: 'orders',
        canActivate: [permissionGuard],
        data: { permission: 'sales.view|sales.pick|sales.create|sales.checkout', feature: 'ordering' },
        loadComponent: () => import('./pages/orders/orders').then((m) => m.Orders),
      },
      {
        path: 'customers',
        canActivate: [permissionGuard],
        data: { permission: 'customers.view' },
        loadComponent: () => import('./pages/customers/customers').then((m) => m.Customers),
      },
      {
        path: 'customers/:id',
        canActivate: [permissionGuard],
        data: { permission: 'customers.view' },
        loadComponent: () =>
          import('./pages/customers/customer-profile').then((m) => m.CustomerProfile),
      },
      {
        path: 'suppliers',
        canActivate: [permissionGuard],
        data: { permission: 'suppliers.view' , feature: 'suppliers' },
        loadComponent: () => import('./pages/suppliers/suppliers').then((m) => m.Suppliers),
      },
      {
        path: 'returns',
        canActivate: [permissionGuard],
        data: { permission: 'returns.view' },
        loadComponent: () => import('./pages/returns/returns').then((m) => m.Returns),
      },
      {
        path: 'suppliers/:id',
        canActivate: [permissionGuard],
        data: { permission: 'suppliers.view' , feature: 'suppliers' },
        loadComponent: () =>
          import('./pages/suppliers/supplier-profile').then((m) => m.SupplierProfile),
      },
      {
        path: 'products',
        canActivate: [permissionGuard],
        data: { permission: 'products.view' },
        loadComponent: () => import('./pages/products/products').then((m) => m.Products),
      },
      {
        path: 'warehouse',
        canActivate: [permissionGuard],
        data: { permission: 'stocks.view' },
        loadComponent: () => import('./pages/warehouse/warehouse').then((m) => m.Warehouse),
      },
      {
        path: 'supplies/new',
        canActivate: [permissionGuard],
        data: { permission: 'supplies.create' , feature: 'supplies' },
        loadComponent: () => import('./pages/supplies/supply-create').then((m) => m.SupplyCreate),
      },
      {
        path: 'supplies/:id/edit',
        canActivate: [permissionGuard],
        data: { permission: 'supplies.edit' , feature: 'supplies' },
        loadComponent: () => import('./pages/supplies/supply-create').then((m) => m.SupplyCreate),
      },
      {
        path: 'supplies',
        canActivate: [permissionGuard],
        data: { permission: 'supplies.view' , feature: 'supplies' },
        loadComponent: () => import('./pages/supplies/supplies').then((m) => m.Supplies),
      },
      {
        path: 'barcode-print',
        canActivate: [permissionGuard],
        data: { permission: 'products.printBarcode' },
        loadComponent: () =>
          import('./pages/barcode-print/barcode-print').then((m) => m.BarcodePrint),
      },
      {
        path: 'transfers',
        canActivate: [permissionGuard],
        data: { permission: 'stock_transfers.view' , feature: 'stock_transfers' },
        loadComponent: () => import('./pages/transfers/transfers').then((m) => m.Transfers),
      },
      {
        path: 'accounts',
        canActivate: [permissionGuard],
        data: { permission: 'accounts.view' , feature: 'accounts' },
        loadComponent: () => import('./pages/accounts/accounts').then((m) => m.Accounts),
      },
      {
        path: 'transactions',
        canActivate: [permissionGuard],
        data: { permission: 'transactions.view' , feature: 'accounts' },
        loadComponent: () =>
          import('./pages/transactions/transactions').then((m) => m.Transactions),
      },
      {
        path: 'reports',
        canActivate: [permissionGuard],
        data: { permission: 'reports.view' , feature: 'reports' },
        loadComponent: () => import('./pages/reports/reports').then((m) => m.Reports),
      },
      {
        path: 'settings',
        loadComponent: () => import('./pages/settings/settings').then((m) => m.Settings),
        canActivateChild: [permissionGuard],
        children: [
          {
            path: 'security',
            loadComponent: () => import('./pages/security/security').then((m) => m.Security),
          },
          {
            path: 'devices',
            data: { permission: 'devices.view' },
            loadComponent: () => import('./pages/devices/devices').then((m) => m.Devices),
          },
          {
            path: 'categories',
            data: { permission: 'categories.view' },
            loadComponent: () => import('./pages/categories/categories').then((m) => m.Categories),
          },
          {
            path: 'units',
            data: { permission: 'units.view' },
            loadComponent: () => import('./pages/units/units').then((m) => m.Units),
          },
          {
            path: 'product-types',
            data: { permission: 'product_types.view' },
            loadComponent: () =>
              import('./pages/product-types/product-types').then((m) => m.ProductTypes),
          },
          {
            path: 'manufacturers',
            data: { permission: 'manufacturers.view' },
            loadComponent: () =>
              import('./pages/manufacturers/manufacturers').then((m) => m.Manufacturers),
          },
          {
            path: 'product-reference',
            data: { permission: 'settings.salesPolicy' },
            loadComponent: () =>
              import('./pages/product-reference/product-reference').then((m) => m.ProductReferenceSettingsPage),
          },
          {
            path: 'business',
            data: { permission: 'business.edit' },
            loadComponent: () =>
              import('./pages/business/business').then((m) => m.BusinessSettings),
          },
          {
            path: 'sales-policy',
            data: { permission: 'settings.salesPolicy' },
            loadComponent: () =>
              import('./pages/sales-policy/sales-policy').then((m) => m.SalesPolicySettings),
          },
          {
            path: 'modules',
            data: { permission: 'business.edit' },
            loadComponent: () => import('./pages/modules/modules').then((m) => m.Modules),
          },
          {
            path: 'branches',
            data: { permission: 'branches.view' },
            loadComponent: () => import('./pages/branches/branches').then((m) => m.Branches),
          },
          {
            path: 'warehouses',
            data: { permission: 'warehouses.view' },
            loadComponent: () => import('./pages/warehouses/warehouses').then((m) => m.Warehouses),
          },
          {
            path: 'users',
            data: { permission: 'users.view' },
            loadComponent: () => import('./pages/users/users').then((m) => m.Users),
          },
          {
            path: 'roles',
            data: { permission: 'roles.view' },
            loadComponent: () => import('./pages/roles/roles').then((m) => m.Roles),
          },
          {
            path: 'permissions-matrix',
            data: { permission: 'roles.assignPermissions' },
            loadComponent: () =>
              import('./pages/permissions-matrix/permissions-matrix').then(
                (m) => m.PermissionsMatrix,
              ),
          },
          {
            path: 'loyalty',
            data: { permission: 'loyalty.view', feature: 'loyalty' },
            loadComponent: () => import('./pages/loyalty/loyalty').then((m) => m.Loyalty),
          },
          {
            path: 'expense-categories',
            data: { permission: 'expense_categories.view' },
            loadComponent: () =>
              import('./pages/expense-categories/expense-categories').then(
                (m) => m.ExpenseCategories,
              ),
          },
          {
            path: 'rates',
            data: { permission: 'rates.view', feature: 'multicurrency' },
            loadComponent: () => import('./pages/rates/rates').then((m) => m.Rates),
          },
          {
            path: 'audit',
            data: { permission: 'audit.view', feature: 'audit' },
            loadComponent: () => import('./pages/audit/audit').then((m) => m.Audit),
          },
          {
            path: 'reminders',
            data: { permission: 'notifications.view' },
            loadComponent: () => import('./pages/reminders/reminders').then((m) => m.Reminders),
          },
          {
            path: 'notification-journal',
            data: { permission: 'notifications.journal.view' },
            loadComponent: () =>
              import('./pages/notification-journal/notification-journal').then(
                (m) => m.NotificationJournal,
              ),
          },
          {
            path: 'sms-gateway',
            data: { permission: 'sms.gateway.edit' },
            loadComponent: () => import('./pages/sms-gateway/sms-gateway').then((m) => m.SmsGateway),
          },
          {
            path: 'license',
            data: { permission: 'features.view' },
            loadComponent: () => import('./pages/license/license').then((m) => m.License),
          },
          { path: 'features', redirectTo: 'license' },
          {
            path: 'integrations',
            data: { permission: 'settings.integrations' },
            loadComponent: () =>
              import('./pages/integrations/integrations').then((m) => m.Integrations),
          },
          {
            path: 'hardware-keys',
            data: { permission: 'keys.view' },
            loadComponent: () =>
              import('./pages/hardware-keys/hardware-keys').then((m) => m.HardwareKeys),
          },
          {
            path: 'receipt-settings',
            data: { permission: 'settings.receipt' },
            loadComponent: () =>
              import('./pages/receipt-settings/receipt-settings').then((m) => m.ReceiptSettings),
          },
          {
            path: 'printing',
            data: {
              permission: 'printing.routes.view|printing.jobs.viewOwn|printing.jobs.viewBranch',
              feature: 'remote_printing',
            },
            loadComponent: () => import('./pages/printing/printing').then((m) => m.Printing),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

export interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  permission: string | null;
  feature?: string;
  requiresMultipleWarehouses?: boolean;
}

export interface NavSection {
  labelKey: string | null;
  items: NavItem[];
}

export const NAV_SECTIONS: NavSection[] = [
  {
    labelKey: null,
    items: [
      {
        labelKey: 'dashboard',
        icon: 'space_dashboard',
        route: '/dashboard',
        permission: 'reports.view',
      },
      {
        labelKey: 'pos',
        icon: 'point_of_sale',
        route: '/pos',
        permission: 'sales.create|sales.checkout',
      },
    ],
  },
  {
    labelKey: 'section_sales',
    items: [
      { labelKey: 'shift', icon: 'schedule', route: '/shift', permission: 'shifts.view' },
      { labelKey: 'sale_history', icon: 'receipt_long', route: '/sales', permission: 'sales.view' },
      { labelKey: 'returns', icon: 'keyboard_return', route: '/returns', permission: 'returns.view' },
      {
        labelKey: 'orders',
        icon: 'shopping_basket',
        route: '/orders',
        permission: 'sales.view|sales.pick|sales.create|sales.checkout',
        feature: 'ordering',
      },
      { labelKey: 'customers', icon: 'group', route: '/customers', permission: 'customers.view' },
    ],
  },
  {
    labelKey: 'section_inventory',
    items: [
      {
        labelKey: 'products',
        icon: 'inventory_2',
        route: '/products',
        permission: 'products.view',
      },
      { labelKey: 'inventory', icon: 'warehouse', route: '/warehouse', permission: 'stocks.view' },
      {
        labelKey: 'supplies',
        icon: 'local_shipping',
        route: '/supplies',
        permission: 'supplies.view',
      },
      {
        labelKey: 'suppliers',
        icon: 'contact_phone',
        route: '/suppliers',
        permission: 'suppliers.view',
      },
      {
        labelKey: 'barcode_print',
        icon: 'barcode_reader',
        route: '/barcode-print',
        permission: 'products.printBarcode',
      },
      {
        labelKey: 'transfers',
        icon: 'swap_horiz',
        route: '/transfers',
        permission: 'stock_transfers.view',
        requiresMultipleWarehouses: true,
      },
    ],
  },
  {
    labelKey: 'section_finance',
    items: [
      {
        labelKey: 'accounts',
        icon: 'account_balance_wallet',
        route: '/accounts',
        permission: 'accounts.view',
      },
      {
        labelKey: 'transactions',
        icon: 'sync_alt',
        route: '/transactions',
        permission: 'transactions.view',
      },
      { labelKey: 'reports', icon: 'insights', route: '/reports', permission: 'reports.view' },
    ],
  },
];

export const SETTINGS_SECTIONS: NavSection[] = [
  {
    labelKey: 'section_catalog',
    items: [
      {
        labelKey: 'categories',
        icon: 'category',
        route: '/settings/categories',
        permission: 'categories.view',
      },
      { labelKey: 'units', icon: 'straighten', route: '/settings/units', permission: 'units.view' },
      {
        labelKey: 'product_types',
        icon: 'style',
        route: '/settings/product-types',
        permission: 'product_types.view',
      },
      {
        labelKey: 'manufacturers',
        icon: 'factory',
        route: '/settings/manufacturers',
        permission: 'manufacturers.view',
      },
    ],
  },
  {
    labelKey: 'settings_org',
    items: [
      {
        labelKey: 'business',
        icon: 'store',
        route: '/settings/business',
        permission: 'business.edit',
      },
      {
        labelKey: 'sales_policy',
        icon: 'balance',
        route: '/settings/sales-policy',
        permission: 'settings.salesPolicy',
      },
      {
        labelKey: 'branch',
        icon: 'apartment',
        route: '/settings/branches',
        permission: 'branches.view',
      },
      {
        labelKey: 'warehouse',
        icon: 'home_storage',
        route: '/settings/warehouses',
        permission: 'warehouses.view',
      },
    ],
  },
  {
    labelKey: 'settings_access',
    items: [
      {
        labelKey: 'users',
        icon: 'manage_accounts',
        route: '/settings/users',
        permission: 'users.view',
      },
      {
        labelKey: 'roles',
        icon: 'shield_person',
        route: '/settings/roles',
        permission: 'roles.view',
      },
      {
        labelKey: 'permissions_matrix',
        icon: 'admin_panel_settings',
        route: '/settings/permissions-matrix',
        permission: 'roles.assignPermissions',
      },
    ],
  },
  {
    labelKey: 'settings_system',
    items: [
      {
        labelKey: 'loyalty',
        icon: 'loyalty',
        route: '/settings/loyalty',
        permission: 'loyalty.view',
      },
      {
        labelKey: 'expense_categories',
        icon: 'payments',
        route: '/settings/expense-categories',
        permission: 'expense_categories.view',
      },
      {
        labelKey: 'receipt_settings',
        icon: 'receipt',
        route: '/settings/receipt-settings',
        permission: 'settings.receipt',
      },
      {
        labelKey: 'printing',
        icon: 'print',
        route: '/settings/printing',
        permission: 'printing.routes.view|printing.jobs.viewOwn|printing.jobs.viewBranch',
      },
      {
        labelKey: 'exchange_rates',
        icon: 'currency_exchange',
        route: '/settings/rates',
        permission: 'rates.view',
      },
      { labelKey: 'audit', icon: 'history', route: '/settings/audit', permission: 'audit.view' },
      {
        labelKey: 'reminders',
        icon: 'notifications_active',
        route: '/settings/reminders',
        permission: 'notifications.view',
      },
      {
        labelKey: 'notification_journal',
        icon: 'mark_email_read',
        route: '/settings/notification-journal',
        permission: 'notifications.journal.view',
      },
      {
        labelKey: 'devices',
        icon: 'devices',
        route: '/settings/devices',
        permission: 'devices.view',
      },
    ],
  },
  {
    labelKey: 'settings_developer',
    items: [
      {
        labelKey: 'tariff_features',
        icon: 'workspace_premium',
        route: '/settings/license',
        permission: 'features.view',
      },
      {
        labelKey: 'integrations',
        icon: 'hub',
        route: '/settings/integrations',
        permission: 'settings.integrations',
      },
      {
        labelKey: 'hardware_keys',
        icon: 'usb',
        route: '/settings/hardware-keys',
        permission: 'keys.view',
      },
    ],
  },
  {
    labelKey: 'section_account',
    items: [{ labelKey: 'security', icon: 'lock', route: '/settings/security', permission: null }],
  },
];

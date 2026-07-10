export interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  permission: string | null;
}

export interface NavSection {
  labelKey: string | null;
  items: NavItem[];
}

export const NAV_SECTIONS: NavSection[] = [
  {
    labelKey: null,
    items: [
      { labelKey: 'dashboard', icon: 'space_dashboard', route: '/dashboard', permission: 'reports.view' },
      { labelKey: 'pos', icon: 'point_of_sale', route: '/pos', permission: 'sales.create' },
    ],
  },
  {
    labelKey: 'section_sales',
    items: [
      { labelKey: 'shift', icon: 'schedule', route: '/shift', permission: 'sales.create' },
      { labelKey: 'sale_history', icon: 'receipt_long', route: '/sales', permission: 'sales.view' },
      { labelKey: 'orders', icon: 'shopping_basket', route: '/orders', permission: 'sales.view' },
      { labelKey: 'customers', icon: 'group', route: '/customers', permission: 'customers.view' },
    ],
  },
  {
    labelKey: 'section_inventory',
    items: [
      { labelKey: 'products', icon: 'inventory_2', route: '/products', permission: 'products.view' },
      { labelKey: 'inventory', icon: 'warehouse', route: '/warehouse', permission: 'stocks.view' },
      { labelKey: 'supplies', icon: 'local_shipping', route: '/supplies', permission: 'supplies.view' },
      { labelKey: 'transfers', icon: 'swap_horiz', route: '/transfers', permission: 'stock_transfers.view' },
    ],
  },
  {
    labelKey: 'section_finance',
    items: [
      { labelKey: 'accounts', icon: 'account_balance_wallet', route: '/accounts', permission: 'accounts.view' },
      { labelKey: 'transactions', icon: 'sync_alt', route: '/transactions', permission: 'transactions.view' },
      { labelKey: 'reports', icon: 'insights', route: '/reports', permission: 'reports.view' },
    ],
  },
];

export const SETTINGS_SECTIONS: NavSection[] = [
  {
    labelKey: 'section_catalog',
    items: [
      { labelKey: 'categories', icon: 'category', route: '/settings/categories', permission: 'categories.manage' },
      { labelKey: 'units', icon: 'straighten', route: '/settings/units', permission: 'products.manage' },
      { labelKey: 'product_types', icon: 'style', route: '/settings/product-types', permission: 'products.manage' },
      { labelKey: 'manufacturers', icon: 'factory', route: '/settings/manufacturers', permission: 'products.manage' },
    ],
  },
  {
    labelKey: 'settings_org',
    items: [
      { labelKey: 'business', icon: 'store', route: '/settings/business', permission: 'business.manage' },
      { labelKey: 'branch', icon: 'apartment', route: '/settings/branches', permission: 'branches.manage' },
      { labelKey: 'warehouse', icon: 'home_storage', route: '/settings/warehouses', permission: 'warehouses.manage' },
      { labelKey: 'suppliers', icon: 'contact_phone', route: '/settings/suppliers', permission: 'suppliers.manage' },
    ],
  },
  {
    labelKey: 'settings_access',
    items: [
      { labelKey: 'users', icon: 'manage_accounts', route: '/settings/users', permission: 'users.view' },
      { labelKey: 'roles', icon: 'shield_person', route: '/settings/roles', permission: 'roles.view' },
    ],
  },
  {
    labelKey: 'settings_system',
    items: [
      { labelKey: 'loyalty', icon: 'loyalty', route: '/settings/loyalty', permission: 'loyalty.view' },
      { labelKey: 'expense_categories', icon: 'payments', route: '/settings/expense-categories', permission: 'settings.manage' },
      { labelKey: 'exchange_rates', icon: 'currency_exchange', route: '/settings/rates', permission: 'rates.manage' },
      { labelKey: 'audit', icon: 'history', route: '/settings/audit', permission: 'audit.view' },
      { labelKey: 'receipt_settings', icon: 'receipt', route: '/settings/receipt-settings', permission: 'business.manage' },
    ],
  },
  {
    labelKey: 'settings_developer',
    items: [
      { labelKey: 'tariff_features', icon: 'workspace_premium', route: '/settings/license', permission: 'settings.manage' },
      { labelKey: 'integrations', icon: 'hub', route: '/settings/integrations', permission: 'settings.manage' },
    ],
  },
];

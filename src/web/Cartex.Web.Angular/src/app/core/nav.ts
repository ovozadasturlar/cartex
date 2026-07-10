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
    labelKey: 'section_catalog',
    items: [
      { labelKey: 'products', icon: 'inventory_2', route: '/products', permission: 'products.view' },
      { labelKey: 'categories', icon: 'category', route: '/categories', permission: 'categories.manage' },
      { labelKey: 'units', icon: 'straighten', route: '/units', permission: 'products.manage' },
      { labelKey: 'product_types', icon: 'style', route: '/product-types', permission: 'products.manage' },
      { labelKey: 'manufacturers', icon: 'factory', route: '/manufacturers', permission: 'products.manage' },
    ],
  },
  {
    labelKey: 'section_inventory',
    items: [
      { labelKey: 'inventory', icon: 'warehouse', route: '/warehouse', permission: 'stocks.view' },
      { labelKey: 'supplies', icon: 'local_shipping', route: '/supplies', permission: 'supplies.view' },
      { labelKey: 'transfers', icon: 'swap_horiz', route: '/transfers', permission: 'stock_transfers.view' },
      { labelKey: 'suppliers', icon: 'contact_phone', route: '/suppliers', permission: 'suppliers.manage' },
    ],
  },
  {
    labelKey: 'section_finance',
    items: [
      { labelKey: 'accounts', icon: 'account_balance_wallet', route: '/accounts', permission: 'accounts.view' },
      { labelKey: 'transactions', icon: 'sync_alt', route: '/transactions', permission: 'transactions.view' },
      { labelKey: 'exchange_rates', icon: 'currency_exchange', route: '/rates', permission: 'rates.manage' },
      { labelKey: 'expense_categories', icon: 'payments', route: '/expense-categories', permission: 'settings.manage' },
      { labelKey: 'reports', icon: 'insights', route: '/reports', permission: 'reports.view' },
    ],
  },
  {
    labelKey: 'section_admin',
    items: [
      { labelKey: 'users', icon: 'manage_accounts', route: '/users', permission: 'users.view' },
      { labelKey: 'roles', icon: 'shield_person', route: '/roles', permission: 'roles.view' },
      { labelKey: 'branch', icon: 'apartment', route: '/branches', permission: 'branches.manage' },
      { labelKey: 'warehouse', icon: 'home_storage', route: '/warehouses', permission: 'warehouses.manage' },
      { labelKey: 'loyalty', icon: 'loyalty', route: '/loyalty', permission: 'loyalty.view' },
      { labelKey: 'business', icon: 'store', route: '/business', permission: 'business.manage' },
      { labelKey: 'audit', icon: 'history', route: '/audit', permission: 'audit.view' },
    ],
  },
];

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
    items: [{ labelKey: 'dashboard', icon: 'space_dashboard', route: '/dashboard', permission: 'reports.view' }],
  },
  {
    labelKey: 'section_sales',
    items: [
      { labelKey: 'sale_history', icon: 'receipt_long', route: '/sales', permission: 'sales.view' },
      { labelKey: 'customers', icon: 'group', route: '/customers', permission: 'customers.view' },
    ],
  },
  {
    labelKey: 'section_catalog',
    items: [{ labelKey: 'products', icon: 'inventory_2', route: '/products', permission: 'products.view' }],
  },
  {
    labelKey: 'section_analytics',
    items: [{ labelKey: 'reports', icon: 'insights', route: '/reports', permission: 'reports.view' }],
  },
];

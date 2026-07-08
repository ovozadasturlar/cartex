export interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  permission: string | null;
}

export const NAV_ITEMS: NavItem[] = [
  { labelKey: 'dashboard', icon: 'dashboard', route: '/dashboard', permission: null },
];

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SalesPolicy } from './api/settings.api';
import { AuthService } from './auth.service';
import { FeaturesService } from './features.service';
import { NAV_SECTIONS, NavItem, SETTINGS_SECTIONS, isNavItemOpen } from './nav';
import { SalesPolicyService } from './sales-policy.service';
import { WarehouseContextService } from './warehouse-context.service';

export const authGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/login']);

/// RUXSAT-02/RUXSAT-04: sahifa ruxsat ham, modul ham ochiq bo'lgandagina ochiladi. BRAK-06 ga
/// ko'ra ba'zi bo'limlar ustiga do'kon siyosati ham qo'shiladi. Menyuda ko'rinmagan sahifaga URL
/// orqali kirib bo'lmasligi kerak — aks holda foydalanuvchi bo'sh yoki xato beradigan ekranga
/// tushadi.
export const permissionGuard: CanActivateFn = async (route) => {
  const auth = inject(AuthService);
  const features = inject(FeaturesService);
  const policies = inject(SalesPolicyService);
  const router = inject(Router);

  const permission = route.data['permission'] as string | undefined;
  if (permission && !auth.hasPermission(permission)) return router.createUrlTree(['/']);

  const policy = route.data['policy'] as keyof SalesPolicy | undefined;
  if (policy) {
    await policies.ensureLoaded();
    if (!policies.has(policy)) return router.createUrlTree(['/']);
  }

  const feature = route.data['feature'] as string | undefined;
  if (!feature) return true;

  await features.ensureLoaded();
  return features.has(feature) || router.createUrlTree(['/']);
};

/// Kirgandan keyin foydalanuvchi **o'ziga ochiq** birinchi bo'limga tushadi: ruxsati yo'q yoki
/// moduli o'chiq sahifaga tushirish uni bo'sh ekran bilan qarshi olardi. Foydalanuvchining
/// saqlangan boshlang'ich sahifasi bo'lsa, avval o'sha tekshiriladi — lekin ruxsat/modul/siyosat
/// o'zgargan bo'lishi mumkin, shuning uchun u ham xuddi menyudagi kabi tasdiqlanadi.
export const landingGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const features = inject(FeaturesService);
  const policies = inject(SalesPolicyService);
  const wh = inject(WarehouseContextService);
  const router = inject(Router);

  await Promise.all([features.ensureLoaded(), policies.ensureLoaded()]);
  const open = (item: NavItem) =>
    isNavItemOpen(item, (p) => auth.hasPermission(p), (f) => features.has(f), (p) => policies.has(p), wh.warehouses().length);

  const startPage = auth.currentUser()?.startPage;
  const saved = startPage
    ? [...NAV_SECTIONS, ...SETTINGS_SECTIONS].flatMap((s) => s.items).find((i) => i.labelKey === startPage)
    : undefined;
  if (saved && open(saved)) return router.createUrlTree([saved.route.startsWith('/') ? saved.route : '/settings/' + saved.route]);

  for (const section of [...NAV_SECTIONS, ...SETTINGS_SECTIONS]) {
    for (const item of section.items) {
      if (!open(item)) continue;
      return router.createUrlTree([item.route.startsWith('/') ? item.route : '/settings/' + item.route]);
    }
  }
  return router.createUrlTree(['/login']);
};

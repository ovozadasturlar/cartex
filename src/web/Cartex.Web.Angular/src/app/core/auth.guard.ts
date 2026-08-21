import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { FeaturesService } from './features.service';
import { NAV_SECTIONS, SETTINGS_SECTIONS } from './nav';

export const authGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/login']);

/// RUXSAT-02/RUXSAT-04: sahifa ruxsat ham, modul ham ochiq bo'lgandagina ochiladi. Menyuda
/// ko'rinmagan sahifaga URL orqali kirib bo'lmasligi kerak — aks holda foydalanuvchi bo'sh yoki
/// xato beradigan ekranga tushadi.
export const permissionGuard: CanActivateFn = async (route) => {
  const auth = inject(AuthService);
  const features = inject(FeaturesService);
  const router = inject(Router);

  const permission = route.data['permission'] as string | undefined;
  if (permission && !auth.hasPermission(permission)) return router.createUrlTree(['/']);

  const feature = route.data['feature'] as string | undefined;
  if (!feature) return true;

  await features.ensureLoaded();
  return features.has(feature) || router.createUrlTree(['/']);
};

/// Kirgandan keyin foydalanuvchi **o'ziga ochiq** birinchi bo'limga tushadi: ruxsati yo'q yoki
/// moduli o'chiq sahifaga tushirish uni bo'sh ekran bilan qarshi olardi.
export const landingGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const features = inject(FeaturesService);
  const router = inject(Router);

  await features.ensureLoaded();
  for (const section of [...NAV_SECTIONS, ...SETTINGS_SECTIONS]) {
    for (const item of section.items) {
      if (item.permission && !auth.hasPermission(item.permission)) continue;
      if (!features.has(item.feature)) continue;
      return router.createUrlTree([item.route.startsWith('/') ? item.route : '/settings/' + item.route]);
    }
  }
  return router.createUrlTree(['/login']);
};

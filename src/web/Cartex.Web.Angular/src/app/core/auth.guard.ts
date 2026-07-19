import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { NAV_SECTIONS, SETTINGS_SECTIONS } from './nav';

export const authGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/login']);

export const permissionGuard: CanActivateFn = (route) => {
  const permission = route.data['permission'] as string | undefined;
  return !permission || inject(AuthService).hasPermission(permission) || inject(Router).createUrlTree(['/']);
};

export const landingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  for (const section of [...NAV_SECTIONS, ...SETTINGS_SECTIONS]) {
    for (const item of section.items) {
      if (!item.permission || auth.hasPermission(item.permission))
        return router.createUrlTree([item.route.startsWith('/') ? item.route : '/settings/' + item.route]);
    }
  }
  return router.createUrlTree(['/login']);
};

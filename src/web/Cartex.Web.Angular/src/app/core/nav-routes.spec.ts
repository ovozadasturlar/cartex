import '@angular/compiler';
import { Route } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { routes } from '../app.routes';
import { NAV_SECTIONS, SETTINGS_SECTIONS } from './nav';

// docs/domain-rules.md RUXSAT-02 / RUXSAT-04 dan yozilgan: menyu va marshrut bir xil
// ruxsat va modul talabini ko'rsatishi shart — aks holda menyuda ko'rinmagan sahifa
// manzil orqali ochilib qoladi yoki aksincha.
function flatten(list: Route[], prefix = ''): Map<string, Route> {
  const found = new Map<string, Route>();
  for (const route of list) {
    const path = [prefix, route.path ?? ''].filter(Boolean).join('/');
    if (route.path !== undefined && !route.path.includes(':')) found.set('/' + path, route);
    for (const [key, child] of flatten(route.children ?? [], path)) found.set(key, child);
  }
  return found;
}

describe('menyu va marshrut mosligi', () => {
  const byRoute = flatten(routes);
  const items = [...NAV_SECTIONS, ...SETTINGS_SECTIONS].flatMap((s) => s.items);

  it.each(items.map((i) => [i.route, i] as const))('%s', (path, item) => {
    const route = byRoute.get(path);
    expect(route, `${path} uchun marshrut topilmadi`).toBeDefined();
    expect(route!.data?.['permission'] ?? null).toBe(item.permission);
    expect(route!.data?.['feature']).toBe(item.feature);
  });
});

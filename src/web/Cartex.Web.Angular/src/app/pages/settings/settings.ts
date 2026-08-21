import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { FeaturesService } from '../../core/features.service';
import { NavSection, SETTINGS_SECTIONS } from '../../core/nav';

@Component({
  selector: 'app-settings',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule, TranslocoModule],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly enabledFeatures = inject(FeaturesService).enabled;

  /// RUXSAT-04: yon menyu qobiq menyusi bilan bir xil qoidada — ruxsat ham, modul ham ochiq
  /// bo'lgandagina. Ro'yxat hali kelmagan bo'lsa modul yopiq deb qaralmaydi.
  readonly sections = computed<NavSection[]>(() => SETTINGS_SECTIONS
    .map((s) => ({
      ...s,
      items: s.items.filter((i) =>
        (!i.permission || this.auth.hasPermission(i.permission))
        && (!i.feature || this.enabledFeatures()?.has(i.feature) !== false)),
    }))
    .filter((s) => s.items.length > 0));

  readonly collapsed = signal(localStorage.getItem('cartex.settingsNav') === '1');
  readonly isPhone = toSignal(
    inject(BreakpointObserver).observe('(max-width: 699px)').pipe(map((result) => result.matches)),
    { initialValue: window.innerWidth <= 699 },
  );
  readonly isTablet = toSignal(
    inject(BreakpointObserver).observe('(min-width: 700px) and (max-width: 1099px)').pipe(map((result) => result.matches)),
    { initialValue: window.innerWidth >= 700 && window.innerWidth <= 1099 },
  );
  readonly compact = computed(() => this.collapsed() || this.isTablet());
  readonly currentRoute = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects.split('?')[0]),
    ),
    { initialValue: this.router.url.split('?')[0] },
  );

  toggle(): void {
    this.collapsed.update((v) => !v);
    localStorage.setItem('cartex.settingsNav', this.collapsed() ? '1' : '0');
  }

  navigate(event: Event): void {
    void this.router.navigateByUrl((event.target as HTMLSelectElement).value);
  }

  constructor() {
    const open = this.sections();
    if (this.router.url === '/settings' && open.length) {
      void this.router.navigateByUrl(open[0].items[0].route, { replaceUrl: true });
    }
  }
}

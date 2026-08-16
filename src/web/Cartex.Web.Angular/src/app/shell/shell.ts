import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, ElementRef, HostListener, computed, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { lastValueFrom, map } from 'rxjs';
import { FeaturesApi } from '../core/api/misc.api';
import { AuthService } from '../core/auth.service';
import { NAV_SECTIONS, NavItem, SETTINGS_SECTIONS } from '../core/nav';
import { APP_LANGUAGES, PreferencesService } from '../core/preferences.service';
import { WarehouseContextService } from '../core/warehouse-context.service';
import { Logo } from '../shared/logo';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatTooltipModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    TranslocoModule,
    Logo,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly featuresApi = inject(FeaturesApi);
  private readonly transloco = inject(TranslocoService);
  readonly preferences = inject(PreferencesService);
  readonly wh = inject(WarehouseContextService);

  private readonly breakpoints = inject(BreakpointObserver);
  readonly isDesktop = toSignal(
    this.breakpoints.observe('(min-width: 1200px)').pipe(map((result) => result.matches)),
    { initialValue: window.innerWidth >= 1200 },
  );
  readonly isPhone = toSignal(
    this.breakpoints.observe('(max-width: 767px)').pipe(map((result) => result.matches)),
    { initialValue: window.innerWidth <= 767 },
  );
  readonly isTablet = computed(() => !this.isDesktop() && !this.isPhone());
  readonly collapsed = signal(localStorage.getItem('cartex.sidenav') === 'collapsed');
  readonly tabletExpanded = signal(false);
  readonly compactNav = computed(
    () => (this.isDesktop() && this.collapsed()) || (this.isTablet() && !this.tabletExpanded()),
  );
  readonly user = this.auth.currentUser;
  readonly enabledFeatures = signal<Set<string> | null>(null);
  readonly navSections = computed(() => NAV_SECTIONS.map((s) => ({
    ...s,
    items: s.items.filter((i) =>
      (i.permission === null || this.auth.hasPermission(i.permission))
      && (!i.feature || this.enabledFeatures()?.has(i.feature) !== false)
      && (!i.requiresMultipleWarehouses || this.wh.warehouses().length > 1)),
  })).filter((s) => s.items.length > 0));
  readonly canOpenSettings = SETTINGS_SECTIONS.some((s) =>
    s.items.some((i) => i.permission === null || this.auth.hasPermission(i.permission)),
  );
  readonly languages = APP_LANGUAGES;
  readonly canPickWarehouse = this.auth.hasPermission('sales.create') || this.auth.hasPermission('stocks.view');
  readonly mobileNav = computed(() => this.navSections().flatMap((section) => section.items).slice(0, 4));
  readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('globalSearch');
  readonly searchQuery = signal('');
  readonly searchItems = computed(() => [...NAV_SECTIONS, ...SETTINGS_SECTIONS]
    .flatMap((section) => section.items)
    .filter((item) =>
      (!item.permission || this.auth.hasPermission(item.permission))
      && (!item.feature || this.enabledFeatures()?.has(item.feature) !== false)
      && (!item.requiresMultipleWarehouses || this.wh.warehouses().length > 1)));
  readonly searchResults = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();
    return query
      ? this.searchItems().filter((item) => {
          this.preferences.language();
          return this.transloco.translate(item.labelKey).toLocaleLowerCase().includes(query)
            || item.labelKey.toLocaleLowerCase().includes(query);
        }).slice(0, 8)
      : [];
  });

  constructor() {
    if (this.canPickWarehouse) void this.wh.load();
    lastValueFrom(this.featuresApi.enabled())
      .then((features) => this.enabledFeatures.set(new Set(features)))
      .catch(() => this.enabledFeatures.set(new Set()));
  }

  onWarehouse(e: Event): void {
    this.wh.select(Number((e.target as HTMLSelectElement).value));
  }

  toggleSidebar(nav: { toggle(): void }): void {
    if (this.isPhone()) {
      nav.toggle();
      return;
    }
    if (this.isTablet()) {
      this.tabletExpanded.update((value) => !value);
      return;
    }
    this.collapsed.update((v) => !v);
    localStorage.setItem('cartex.sidenav', this.collapsed() ? 'collapsed' : 'open');
  }

  get activeLang(): string {
    return this.preferences.language();
  }

  setLang(code: string): void {
    this.preferences.setLanguage(code);
  }

  toggleTheme(): void {
    this.preferences.toggleTheme();
  }

  openSearchItem(item: NavItem): void {
    this.searchQuery.set('');
    void this.router.navigateByUrl(item.route);
  }

  @HostListener('document:keydown', ['$event'])
  onShortcut(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === 'k') {
      event.preventDefault();
      this.searchInput()?.nativeElement.focus();
    }
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}

import { Component, ElementRef, HostListener, computed, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../core/auth.service';
import { FeaturesService } from '../core/features.service';
import { LayoutService } from '../core/layout.service';
import { NAV_SECTIONS, NavItem, SETTINGS_SECTIONS, phoneNavItems } from '../core/nav';
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
  private readonly transloco = inject(TranslocoService);
  readonly preferences = inject(PreferencesService);
  readonly wh = inject(WarehouseContextService);

  private readonly layout = inject(LayoutService);
  readonly isDesktop = this.layout.isDesktop;
  readonly isPhone = this.layout.isPhone;
  readonly isTablet = this.layout.isTablet;
  readonly collapsed = signal(localStorage.getItem('cartex.sidenav') === 'collapsed');
  readonly tabletExpanded = signal(false);
  readonly compactNav = computed(
    () => (this.isDesktop() && this.collapsed()) || (this.isTablet() && !this.tabletExpanded()),
  );
  readonly user = this.auth.currentUser;
  private readonly featuresService = inject(FeaturesService);
  readonly navSections = computed(() => NAV_SECTIONS.map((s) => ({
    ...s,
    items: s.items.filter((i) =>
      (i.permission === null || this.auth.hasPermission(i.permission))
      && this.featuresService.has(i.feature)
      && (!i.requiresMultipleWarehouses || this.wh.warehouses().length > 1)),
  })).filter((s) => s.items.length > 0));
  /// RUXSAT-04: sozlamalar tugmasi faqat ichida ochiq sahifa bo'lsa ko'rinadi — modul yopiq
  /// bo'lsa o'sha sahifa hisobga olinmaydi.
  readonly canOpenSettings = computed(() => SETTINGS_SECTIONS.some((s) =>
    s.items.some((i) =>
      (i.permission === null || this.auth.hasPermission(i.permission))
      && this.featuresService.has(i.feature)),
  ));
  readonly languages = APP_LANGUAGES;
  readonly canPickWarehouse = this.auth.hasPermission('sales.create') || this.auth.hasPermission('stocks.view');
  /// Telefonda pastki panelga 4 ta joy bor, shuning uchun sahifalar menyudagi tartibda emas,
  /// telefonda haqiqatan kerak bo'ladigan tartibda tanlanadi (`PHONE_NAV_ORDER`). Ro'yxat
  /// foydalanuvchiga moslashadi: hisobot ruxsati yo'q kassir kassa/savdo/mijoz/mahsulotni
  /// ko'radi, egaga boshqaruv paneli ham tushadi. Qolgani "Ko'proq" menyusida.
  readonly mobileNav = computed(() =>
    phoneNavItems(this.navSections().flatMap((section) => section.items)));
  readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('globalSearch');
  readonly searchQuery = signal('');
  readonly searchItems = computed(() => [...NAV_SECTIONS, ...SETTINGS_SECTIONS]
    .flatMap((section) => section.items)
    .filter((item) =>
      (!item.permission || this.auth.hasPermission(item.permission))
      && this.featuresService.has(item.feature)
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
    void this.featuresService.ensureLoaded();
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

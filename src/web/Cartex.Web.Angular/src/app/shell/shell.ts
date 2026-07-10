import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { map } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { NAV_SECTIONS, SETTINGS_SECTIONS } from '../core/nav';
import { WarehouseContextService } from '../core/warehouse-context.service';
import { Logo } from '../shared/logo';

const LANGS: Record<string, string> = {
  'uz-latn': "O'zbekcha",
  'uz-cyrl': 'Ўзбекча',
  ru: 'Русский',
  en: 'English',
};

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
  readonly wh = inject(WarehouseContextService);

  readonly isWide = toSignal(
    inject(BreakpointObserver).observe('(min-width: 1280px)').pipe(map((r) => r.matches)),
    { initialValue: window.innerWidth >= 1280 },
  );
  readonly isDark = signal(localStorage.getItem('cartex.theme') === 'dark');
  readonly collapsed = signal(localStorage.getItem('cartex.sidenav') === 'collapsed');
  readonly user = this.auth.currentUser;
  readonly navSections = NAV_SECTIONS.map((s) => ({
    ...s,
    items: s.items.filter((i) => i.permission === null || this.auth.hasPermission(i.permission)),
  })).filter((s) => s.items.length > 0);
  readonly canOpenSettings = SETTINGS_SECTIONS.some((s) =>
    s.items.some((i) => i.permission === null || this.auth.hasPermission(i.permission)),
  );
  readonly languages = Object.entries(LANGS).map(([code, name]) => ({ code, name }));
  readonly canPickWarehouse = this.auth.hasPermission('sales.create') || this.auth.hasPermission('stocks.view');

  constructor() {
    document.body.classList.toggle('dark', this.isDark());
    if (this.canPickWarehouse) this.wh.load();
  }

  onWarehouse(e: Event): void {
    this.wh.select(Number((e.target as HTMLSelectElement).value));
  }

  toggleSidebar(nav: { toggle(): void }): void {
    if (!this.isWide()) {
      nav.toggle();
      return;
    }
    this.collapsed.update((v) => !v);
    localStorage.setItem('cartex.sidenav', this.collapsed() ? 'collapsed' : 'open');
  }

  get activeLang(): string {
    return this.transloco.getActiveLang();
  }

  setLang(code: string): void {
    this.transloco.setActiveLang(code);
    localStorage.setItem('cartex.lang', code);
  }

  toggleTheme(): void {
    this.isDark.update((v) => !v);
    localStorage.setItem('cartex.theme', this.isDark() ? 'dark' : 'light');
    document.body.classList.toggle('dark', this.isDark());
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}

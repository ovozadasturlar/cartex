import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { map } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { NAV_SECTIONS } from '../core/nav';
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
    MatToolbarModule,
    MatListModule,
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

  readonly isWide = toSignal(
    inject(BreakpointObserver).observe('(min-width: 1280px)').pipe(map((r) => r.matches)),
    { initialValue: window.innerWidth >= 1280 },
  );
  readonly isDark = signal(localStorage.getItem('cartex.theme') === 'dark');
  readonly user = this.auth.currentUser;
  readonly navSections = NAV_SECTIONS.map((s) => ({
    ...s,
    items: s.items.filter((i) => i.permission === null || this.auth.hasPermission(i.permission)),
  })).filter((s) => s.items.length > 0);
  readonly languages = Object.entries(LANGS).map(([code, name]) => ({ code, name }));

  constructor() {
    document.body.classList.toggle('dark', this.isDark());
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

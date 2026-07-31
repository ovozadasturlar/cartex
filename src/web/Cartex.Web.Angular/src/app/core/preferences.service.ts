import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

export type AppTheme = 'light' | 'dark';

export interface AppLanguage {
  code: string;
  name: string;
  short: string;
}

export const APP_LANGUAGES: AppLanguage[] = [
  { code: 'uz-latn', name: "O'zbekcha", short: 'UZ' },
  { code: 'uz-cyrl', name: 'Ўзбекча', short: 'ЎЗ' },
  { code: 'ru', name: 'Русский', short: 'RU' },
  { code: 'en', name: 'English', short: 'EN' },
];

@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private readonly transloco = inject(TranslocoService);

  readonly theme = signal<AppTheme>(localStorage.getItem('cartex.theme') === 'dark' ? 'dark' : 'light');
  readonly language = signal(localStorage.getItem('cartex.lang') ?? 'uz-latn');
  readonly isDark = computed(() => this.theme() === 'dark');
  readonly languageShort = computed(
    () => APP_LANGUAGES.find((item) => item.code === this.language())?.short ?? 'UZ',
  );

  constructor() {
    this.applyTheme();
  }

  setTheme(theme: AppTheme): void {
    this.theme.set(theme);
    localStorage.setItem('cartex.theme', theme);
    this.applyTheme();
  }

  toggleTheme(): void {
    this.setTheme(this.isDark() ? 'light' : 'dark');
  }

  setLanguage(code: string): void {
    if (!APP_LANGUAGES.some((item) => item.code === code)) return;
    this.language.set(code);
    localStorage.setItem('cartex.lang', code);
    this.transloco.setActiveLang(code);
    document.documentElement.lang = code;
  }

  private applyTheme(): void {
    document.body.classList.toggle('dark', this.isDark());
    document.documentElement.style.colorScheme = this.theme();
  }
}

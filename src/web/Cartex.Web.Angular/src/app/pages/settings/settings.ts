import { Component, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from '../../core/auth.service';
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

  readonly sections: NavSection[] = SETTINGS_SECTIONS
    .map((s) => ({ ...s, items: s.items.filter((i) => !i.permission || this.auth.hasPermission(i.permission)) }))
    .filter((s) => s.items.length > 0);

  readonly collapsed = signal(localStorage.getItem('cartex.settingsNav') === '1');

  toggle(): void {
    this.collapsed.update((v) => !v);
    localStorage.setItem('cartex.settingsNav', this.collapsed() ? '1' : '0');
  }

  constructor() {
    if (this.router.url === '/settings' && this.sections.length) {
      this.router.navigateByUrl(this.sections[0].items[0].route, { replaceUrl: true });
    }
  }
}

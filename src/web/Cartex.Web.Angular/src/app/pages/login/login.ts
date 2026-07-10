import { Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import QRCode from 'qrcode';
import { AuthService } from '../../core/auth.service';
import { Logo } from '../../shared/logo';

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms));

@Component({
  selector: 'app-login',
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatProgressBarModule,
    TranslocoModule,
    Logo,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  username = '';
  password = '';
  rememberMe = false;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly qrAvailable = signal(false);
  readonly qrOpen = signal(false);
  readonly qrDataUrl = signal<string | null>(null);
  readonly qrProgress = signal(100);
  private qrSession = 0;
  private qrTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.auth.loginMethods().then(
      (m) => this.qrAvailable.set(m.qrEnabled),
      () => {},
    );
  }

  ngOnDestroy(): void {
    this.qrSession++;
  }

  async submit(): Promise<void> {
    if (!this.username.trim() || !this.password || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.username.trim(), this.password, this.rememberMe);
      this.router.navigate(['/']);
    } catch {
      this.error.set('login_failed');
    } finally {
      this.busy.set(false);
    }
  }

  openQr(): void {
    this.error.set(null);
    this.qrOpen.set(true);
    this.runQr(++this.qrSession);
  }

  closeQr(): void {
    this.qrSession++;
    this.qrOpen.set(false);
    this.qrDataUrl.set(null);
  }

  private startQrTimer(issuedAt: number, lifetime: number, session: number): void {
    if (this.qrTimer) clearInterval(this.qrTimer);
    this.qrProgress.set(100);
    this.qrTimer = setInterval(() => {
      if (session !== this.qrSession) {
        clearInterval(this.qrTimer!);
        return;
      }
      this.qrProgress.set(Math.max(0, 100 - ((Date.now() - issuedAt) / lifetime) * 100));
    }, 200);
  }

  private async runQr(session: number): Promise<void> {
    while (session === this.qrSession) {
      let start;
      try {
        start = await this.auth.startQr();
      } catch {
        await delay(3000);
        continue;
      }
      if (session !== this.qrSession) return;
      this.qrDataUrl.set(await QRCode.toDataURL(`cartexqr:${start.code}`, { width: 232, margin: 1 }));
      const lifetime = Math.max(30, start.expiresInSeconds - 5) * 1000;
      const issuedAt = Date.now();
      this.startQrTimer(issuedAt, lifetime, session);
      while (session === this.qrSession && Date.now() - issuedAt < lifetime) {
        await delay(2000);
        if (session !== this.qrSession) return;
        try {
          if (await this.auth.pollQr(start.code)) {
            if (session !== this.qrSession) return;
            this.closeQr();
            this.router.navigate(['/']);
            return;
          }
        } catch {}
      }
    }
  }
}

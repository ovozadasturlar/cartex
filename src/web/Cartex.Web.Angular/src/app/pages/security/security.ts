import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-security',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslocoModule,
    PageHeader,
  ],
  template: `
    <ng-container *transloco="let t">
      <cx-page-header [title]="t('security')" [subtitle]="t('security_subtitle')" />
      <div class="cx-card card">
        <h3>{{ t('change_password') }}</h3>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('current_password') }}</mat-label>
          <input matInput [type]="show() ? 'text' : 'password'" [(ngModel)]="current" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('new_password') }}</mat-label>
          <input matInput [type]="show() ? 'text' : 'password'" [(ngModel)]="next" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('repeat_password') }}</mat-label>
          <input matInput [type]="show() ? 'text' : 'password'" [(ngModel)]="repeat" />
          <button matIconButton matSuffix type="button" (click)="show.set(!show())">
            <mat-icon>{{ show() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
        </mat-form-field>
        <div class="actions">
          <button matButton="filled" [disabled]="busy()" (click)="save(t)">
            <mat-icon>save</mat-icon>
            {{ t('save') }}
          </button>
        </div>
      </div>
    </ng-container>
  `,
  styles: `
    :host { display: block; }
    .card {
      max-width: 460px;
      padding: 22px;
      display: flex;
      flex-direction: column;
      gap: 12px;

      h3 { margin: 0; font-size: 15px; font-weight: 600; }
    }
    .actions { display: flex; justify-content: flex-end; }
  `,
})
export class Security {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly busy = signal(false);
  readonly show = signal(false);
  current = '';
  next = '';
  repeat = '';

  async save(t: (key: string) => string): Promise<void> {
    if (!this.current || !this.next) {
      this.notify.error(t('err_fill_all'));
      return;
    }
    if (this.next.length < 6) {
      this.notify.error(t('err_password_short'));
      return;
    }
    if (this.next !== this.repeat) {
      this.notify.error(t('err_password_mismatch'));
      return;
    }
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.http.post<void>('/api/auth/change-password', {
          currentPassword: this.current,
          newPassword: this.next,
        }),
      );
      this.current = this.next = this.repeat = '';
      this.notify.success(this.transloco.translate('password_changed'));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

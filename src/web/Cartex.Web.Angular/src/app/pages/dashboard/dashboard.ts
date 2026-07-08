import { Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h1>{{ t('dashboard') }}</h1>
      <mat-card appearance="outlined" class="welcome">
        <mat-card-content>{{ user()?.fullName }}</mat-card-content>
      </mat-card>
    </ng-container>
  `,
  styles: `
    h1 { font: var(--mat-sys-headline-small); margin: 0 0 16px; }
    .welcome { max-width: 480px; }
  `,
})
export class Dashboard {
  readonly user = inject(AuthService).currentUser;
}

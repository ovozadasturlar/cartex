import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Unit, UnitsApi } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-units',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTooltipModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './units.html',
  styleUrl: './units.scss',
})
export class Units implements OnInit {
  private readonly api = inject(UnitsApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly canManage = inject(AuthService).hasPermission('products.manage');
  readonly loading = signal(true);
  readonly all = signal<Unit[]>([]);
  readonly search = signal('');
  readonly dimensions = ['Count', 'Weight', 'Volume', 'Length'];
  readonly groups = computed(() => {
    const q = this.search().toLowerCase();
    const filtered = q ? this.all().filter((u) => u.name.toLowerCase().includes(q)) : this.all();
    return this.dimensions
      .map((d) => ({ dimension: d, units: filtered.filter((u) => u.dimension === d) }))
      .filter((g) => g.units.length > 0 || !q);
  });

  ngOnInit(): void {
    this.load();
  }

  openCreate(dimension?: string): void {
    this.openDialog(null, dimension);
  }

  openEdit(unit: Unit): void {
    if (this.canManage && !unit.isSystem) this.openDialog(unit);
  }

  async toggleEnabled(unit: Unit, event: MatSlideToggleChange): Promise<void> {
    try {
      await lastValueFrom(this.api.setState(unit.id, event.checked, unit.isDefault));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  async makeDefault(unit: Unit, event: Event): Promise<void> {
    event.stopPropagation();
    if (unit.isDefault) return;
    try {
      await lastValueFrom(this.api.setState(unit.id, true, true));
      await this.load();
    } catch (e) {
      this.notify.error(e);
    }
  }

  private openDialog(unit: Unit | null, dimension?: string): void {
    this.dialog
      .open(UnitDialog, { data: { unit, dimension: dimension ?? null }, width: '440px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.load();
      });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.all.set(await lastValueFrom(this.api.all()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}

@Component({
  selector: 'app-unit-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrl: './units.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ unit ? t('edit') : t('add') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('short_name') }}</mat-label>
          <input matInput [(ngModel)]="shortName" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('dimension') }}</mat-label>
          <mat-select [(ngModel)]="dimension">
            @for (d of dimensions; track d) {
              <mat-option [value]="d">{{ t('dimension_' + d.toLowerCase()) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('factor') }}</mat-label>
          <input matInput type="number" min="0" [(ngModel)]="factor" />
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || !name.trim() || !shortName.trim()" (click)="save()">
          {{ t('save') }}
        </button>
      </div>
    </div>
  `,
})
export class UnitDialog {
  private readonly api = inject(UnitsApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<UnitDialog>>(MatDialogRef);

  private readonly data = inject<{ unit: Unit | null; dimension: string | null }>(MAT_DIALOG_DATA);
  readonly unit = this.data.unit;
  readonly busy = signal(false);
  readonly dimensions = ['Count', 'Weight', 'Volume', 'Length'];

  name = this.unit?.name ?? '';
  shortName = this.unit?.shortName ?? '';
  dimension = this.unit?.dimension ?? this.data.dimension ?? 'Count';
  factor = this.unit?.factor ?? 1;

  async save(): Promise<void> {
    if (!this.name.trim() || !this.shortName.trim()) return;
    this.busy.set(true);
    try {
      const body = {
        name: this.name.trim(),
        shortName: this.shortName.trim(),
        dimension: this.dimension,
        factor: this.factor > 0 ? this.factor : 1,
      };
      if (this.unit) await lastValueFrom(this.api.update(this.unit.id, body));
      else await lastValueFrom(this.api.create(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

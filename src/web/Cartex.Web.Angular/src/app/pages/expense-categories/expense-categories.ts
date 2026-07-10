import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { ExpenseCategoriesApi, ExpenseCategory } from '../../core/api/finance.api';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-expense-categories',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './expense-categories.html',
  styleUrl: './expense-categories.scss',
})
export class ExpenseCategories implements OnInit {
  private readonly api = inject(ExpenseCategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly categories = signal<ExpenseCategory[]>([]);
  readonly columns = ['name', 'actions'];

  async ngOnInit(): Promise<void> {
    await this.reload();
    this.loading.set(false);
  }

  open(category?: ExpenseCategory): void {
    this.dialog
      .open(ExpenseCategoryDialog, { data: category ?? null, width: '420px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.reload();
      });
  }

  private async reload(): Promise<void> {
    try {
      this.categories.set(await lastValueFrom(this.api.list()));
    } catch (e) {
      this.notify.error(e);
    }
  }
}

@Component({
  selector: 'app-expense-category-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t(category ? 'edit' : 'add') }} — {{ t('expense_category') }}</h2>
      <mat-dialog-content>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" required />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="saving() || !name.trim()" (click)="save()">
          {{ t('save') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .full { width: 100%; }
  `,
})
export class ExpenseCategoryDialog {
  private readonly api = inject(ExpenseCategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<ExpenseCategoryDialog>);

  readonly category = inject<ExpenseCategory | null>(MAT_DIALOG_DATA);
  readonly saving = signal(false);

  name = this.category?.name ?? '';

  async save(): Promise<void> {
    const name = this.name.trim();
    if (!name) return;
    this.saving.set(true);
    try {
      if (this.category) await lastValueFrom(this.api.update(this.category.id, name));
      else await lastValueFrom(this.api.create(name));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.saving.set(false);
    }
  }
}

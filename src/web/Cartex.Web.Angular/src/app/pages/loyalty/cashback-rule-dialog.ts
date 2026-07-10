import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CashbackRule, LoyaltyApi, NamedOption, ProductOption } from '../../core/api/misc.api';
import { NotifyService } from '../../core/notify.service';

export interface CashbackRuleData {
  rule: CashbackRule | null;
  products: ProductOption[];
  categories: NamedOption[];
}

@Component({
  selector: 'app-cashback-rule-dialog',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrl: './loyalty.scss',
  template: `
    <ng-container *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(data.rule ? 'edit' : 'add_rule') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content class="form">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('scope') }}</mat-label>
          <mat-select [value]="scope()" (selectionChange)="setScope($event.value)">
            <mat-option value="Product">{{ t('product') }}</mat-option>
            <mat-option value="Category">{{ t('category') }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t(scope() === 'Product' ? 'product' : 'category') }}</mat-label>
          <input matInput [ngModel]="targetText()" (ngModelChange)="onTargetText($event)" [matAutocomplete]="auto" />
          <mat-autocomplete #auto [displayWith]="display" (optionSelected)="target = $event.option.value">
            @for (o of options(); track o.id) {
              <mat-option [value]="o">{{ o.name }}</mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>
        <div class="row2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('method') }}</mat-label>
            <mat-select [(ngModel)]="method">
              <mat-option value="Percent">{{ t('cashback_method_percent') }}</mat-option>
              <mat-option value="FixedPerUnit">{{ t('cashback_method_fixed') }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('value') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="value" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('priority') }}</mat-label>
          <input matInput type="number" [(ngModel)]="priority" />
        </mat-form-field>
        <mat-checkbox [(ngModel)]="exclude">{{ t('exclude_from_total') }}</mat-checkbox>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy()" (click)="save(t('success'))">{{ t('save') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
})
export class CashbackRuleDialog {
  private readonly api = inject(LoyaltyApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<CashbackRuleDialog>);

  readonly data = inject<CashbackRuleData>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  readonly scope = signal(this.data.rule?.scope ?? 'Product');
  readonly targetText = signal('');

  target: ProductOption | NamedOption | null = null;
  method = this.data.rule?.method ?? 'Percent';
  value = this.data.rule?.value ?? 0;
  priority = this.data.rule?.priority ?? 0;
  exclude = this.data.rule?.excludeFromTotalPercent ?? false;

  readonly options = computed(() => {
    const list: (ProductOption | NamedOption)[] = this.scope() === 'Product' ? this.data.products : this.data.categories;
    const q = this.targetText().toLowerCase();
    return (q ? list.filter((o) => o.name.toLowerCase().includes(q)) : list).slice(0, 50);
  });

  readonly display = (o: NamedOption | string | null) => (typeof o === 'string' ? o : (o?.name ?? ''));

  constructor() {
    const r = this.data.rule;
    if (r) {
      const list = r.scope === 'Product' ? this.data.products : this.data.categories;
      this.target = list.find((o) => o.id === r.targetId) ?? { id: r.targetId, name: r.targetName };
      this.targetText.set(this.target.name);
    }
  }

  setScope(value: string): void {
    this.scope.set(value);
    this.target = null;
    this.targetText.set('');
  }

  onTargetText(value: NamedOption | string): void {
    if (typeof value === 'string') {
      this.target = null;
      this.targetText.set(value);
    }
  }

  async save(message: string): Promise<void> {
    if (!this.target || this.value <= 0) return;
    this.busy.set(true);
    const body = {
      scope: this.scope(),
      targetId: this.target.id,
      method: this.method,
      value: this.value,
      priority: this.priority,
      excludeFromTotalPercent: this.exclude,
    };
    try {
      if (this.data.rule) await lastValueFrom(this.api.updateRule(this.data.rule.id, body));
      else await lastValueFrom(this.api.createRule(body));
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
      this.busy.set(false);
    }
  }
}

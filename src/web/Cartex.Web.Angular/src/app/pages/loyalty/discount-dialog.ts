import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CustomerOption, DiscountRule, LookupsApi, LoyaltyApi, NamedOption, ProductOption } from '../../core/api/misc.api';
import { NotifyService } from '../../core/notify.service';

export interface DiscountDialogData {
  rule: DiscountRule | null;
  products: ProductOption[];
  categories: NamedOption[];
  manufacturers: NamedOption[];
}

interface ExceptionChip {
  scope: string;
  targetId: number;
  name: string;
}

@Component({
  selector: 'app-discount-dialog',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
  ],
  styleUrl: './loyalty.scss',
  template: `
    <ng-container *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t(data.rule ? 'edit' : 'add') }} · {{ t('discount') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content class="form">
        <div class="row2 center">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('name') }}</mat-label>
            <input matInput [(ngModel)]="name" />
          </mat-form-field>
          <mat-slide-toggle [(ngModel)]="enabled">{{ t('active') }}</mat-slide-toggle>
        </div>
        <div class="row2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('scope') }}</mat-label>
            <mat-select [value]="scope()" (selectionChange)="setScope($event.value)">
              <mat-option value="All">{{ t('discount_scope_all') }}</mat-option>
              <mat-option value="Product">{{ t('product') }}</mat-option>
              <mat-option value="Category">{{ t('category') }}</mat-option>
              <mat-option value="Manufacturer">{{ t('manufacturer') }}</mat-option>
            </mat-select>
          </mat-form-field>
          @if (scope() !== 'All') {
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ t(scope().toLowerCase()) }}</mat-label>
              <input matInput [ngModel]="targetText()" (ngModelChange)="onTargetText($event)" [matAutocomplete]="targetAuto" />
              <mat-autocomplete #targetAuto [displayWith]="display" (optionSelected)="target = $event.option.value">
                @for (o of targetOptions(); track o.id) {
                  <mat-option [value]="o">{{ o.name }}</mat-option>
                }
              </mat-autocomplete>
            </mat-form-field>
          }
        </div>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('customer') }}</mat-label>
          <input matInput [ngModel]="customerText()" (ngModelChange)="onCustomerText($event)" [matAutocomplete]="customerAuto" />
          @if (customer) {
            <button matSuffix matIconButton (click)="clearCustomer()"><mat-icon>close</mat-icon></button>
          }
          <mat-autocomplete #customerAuto [displayWith]="displayCustomer" (optionSelected)="customer = $event.option.value">
            @for (c of customerResults(); track c.id) {
              <mat-option [value]="c">{{ customerName(c) }}</mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>
        <div class="row2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('method') }}</mat-label>
            <mat-select [(ngModel)]="method">
              <mat-option value="Percent">{{ t('cashback_method_percent') }}</mat-option>
              <mat-option value="FixedAmount">{{ t('discount_method_amount') }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('value') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="value" />
          </mat-form-field>
        </div>
        <div class="row2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('discount_min_amount') }}</mat-label>
            <input matInput type="number" min="0" [(ngModel)]="minAmount" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('priority') }}</mat-label>
            <input matInput type="number" [(ngModel)]="priority" />
          </mat-form-field>
        </div>
        <div class="row2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('discount_starts') }}</mat-label>
            <input matInput type="date" [(ngModel)]="startsOn" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('discount_ends') }}</mat-label>
            <input matInput type="date" [(ngModel)]="endsOn" />
          </mat-form-field>
        </div>

        <h3>{{ t('discount_exceptions') }}</h3>
        <div class="row2 exc">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('scope') }}</mat-label>
            <mat-select [value]="excScope()" (selectionChange)="setExcScope($event.value)">
              <mat-option value="Product">{{ t('product') }}</mat-option>
              <mat-option value="Category">{{ t('category') }}</mat-option>
              <mat-option value="Manufacturer">{{ t('manufacturer') }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ t('search') }}</mat-label>
            <input matInput [ngModel]="excText()" (ngModelChange)="onExcText($event)" [matAutocomplete]="excAuto" />
            <mat-autocomplete #excAuto [displayWith]="display" (optionSelected)="addException($event.option.value)">
              @for (o of excOptions(); track o.id) {
                <mat-option [value]="o">{{ o.name }}</mat-option>
              }
            </mat-autocomplete>
          </mat-form-field>
        </div>
        @if (exceptions().length) {
          <div class="chips">
            @for (e of exceptions(); track e.scope + e.targetId) {
              <span class="cx-chip">
                {{ e.name }}
                <button matIconButton (click)="removeException(e)"><mat-icon>close</mat-icon></button>
              </span>
            }
          </div>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="busy()" (click)="save(t('success'), t('discount_target_required'))">{{ t('save') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
})
export class DiscountDialog {
  private readonly api = inject(LoyaltyApi);
  private readonly lookups = inject(LookupsApi);
  private readonly notify = inject(NotifyService);
  private readonly ref = inject(MatDialogRef<DiscountDialog>);
  private searchTimer?: ReturnType<typeof setTimeout>;

  readonly data = inject<DiscountDialogData>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  readonly scope = signal(this.data.rule?.scope ?? 'All');
  readonly targetText = signal('');
  readonly customerText = signal('');
  readonly customerResults = signal<CustomerOption[]>([]);
  readonly excScope = signal('Product');
  readonly excText = signal('');
  readonly exceptions = signal<ExceptionChip[]>(
    (this.data.rule?.exceptions ?? []).map((e) => ({ scope: e.scope, targetId: e.targetId, name: e.targetName })),
  );

  target: NamedOption | null = null;
  customer: CustomerOption | null = null;
  name = this.data.rule?.name ?? '';
  enabled = this.data.rule?.isEnabled ?? true;
  method = this.data.rule?.method ?? 'Percent';
  value = this.data.rule?.value ?? 0;
  minAmount = this.data.rule?.minAmount ?? 0;
  priority = this.data.rule?.priority ?? 0;
  startsOn = this.data.rule?.startsOn ?? '';
  endsOn = this.data.rule?.endsOn ?? '';

  readonly targetOptions = computed(() => this.filter(this.scope(), this.targetText()));
  readonly excOptions = computed(() => this.filter(this.excScope(), this.excText()));

  readonly display = (o: NamedOption | string | null) => (typeof o === 'string' ? o : (o?.name ?? ''));
  readonly displayCustomer = (c: CustomerOption | string | null) => (typeof c === 'string' ? c : c ? this.customerName(c) : '');

  constructor() {
    const r = this.data.rule;
    if (r?.targetId != null && r.targetName) {
      this.target = { id: r.targetId, name: r.targetName };
      this.targetText.set(r.targetName);
    }
    if (r?.customerId != null) {
      this.customer = { id: r.customerId, fullName: r.customerName ?? '', lastName: null, phone: null };
      this.customerText.set(r.customerName ?? '');
    }
  }

  customerName(c: CustomerOption): string {
    return c.lastName ? `${c.fullName} ${c.lastName}` : c.fullName;
  }

  private filter(scope: string, text: string): NamedOption[] {
    const list: NamedOption[] =
      scope === 'Product' ? this.data.products : scope === 'Category' ? this.data.categories : this.data.manufacturers;
    const q = text.toLowerCase();
    return (q ? list.filter((o) => o.name.toLowerCase().includes(q)) : list).slice(0, 50);
  }

  setScope(value: string): void {
    this.scope.set(value);
    this.target = null;
    this.targetText.set('');
  }

  setExcScope(value: string): void {
    this.excScope.set(value);
    this.excText.set('');
  }

  onTargetText(value: NamedOption | string): void {
    if (typeof value === 'string') {
      this.target = null;
      this.targetText.set(value);
    }
  }

  onExcText(value: NamedOption | string): void {
    if (typeof value === 'string') this.excText.set(value);
  }

  onCustomerText(value: CustomerOption | string): void {
    if (typeof value !== 'string') return;
    this.customer = null;
    this.customerText.set(value);
    clearTimeout(this.searchTimer);
    const q = value.trim();
    if (q.length < 2) {
      this.customerResults.set([]);
      return;
    }
    this.searchTimer = setTimeout(async () => {
      try {
        this.customerResults.set(await lastValueFrom(this.lookups.customers(q)));
      } catch {
        this.customerResults.set([]);
      }
    }, 300);
  }

  clearCustomer(): void {
    this.customer = null;
    this.customerText.set('');
    this.customerResults.set([]);
  }

  addException(o: NamedOption): void {
    const scope = this.excScope();
    if (!this.exceptions().some((e) => e.scope === scope && e.targetId === o.id))
      this.exceptions.update((list) => [...list, { scope, targetId: o.id, name: o.name }]);
    this.excText.set('');
  }

  removeException(chip: ExceptionChip): void {
    this.exceptions.update((list) => list.filter((e) => e !== chip));
  }

  async save(message: string, targetRequired: string): Promise<void> {
    if (!this.name.trim() || this.value <= 0) return;
    if (this.scope() !== 'All' && !this.target) {
      this.notify.error(targetRequired);
      return;
    }
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.saveDiscount({
          id: this.data.rule?.id ?? 0,
          name: this.name.trim(),
          isEnabled: this.enabled,
          scope: this.scope(),
          targetId: this.scope() === 'All' ? null : (this.target?.id ?? null),
          customerId: this.customer?.id ?? null,
          minAmount: this.minAmount || 0,
          method: this.method,
          value: this.value,
          priority: this.priority || 0,
          startsOn: this.startsOn || null,
          endsOn: this.endsOn || null,
          exceptions: this.exceptions().map((e) => ({ scope: e.scope, targetId: e.targetId })),
        }),
      );
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
      this.busy.set(false);
    }
  }
}

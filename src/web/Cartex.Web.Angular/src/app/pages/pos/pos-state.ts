import { Injectable, signal } from '@angular/core';
import { Customer } from '../../core/models';

export interface CartLine {
  variantId: number;
  name: string;
  unitName: string;
  price: number;
  qty: number;
  available: number;
}

@Injectable({ providedIn: 'root' })
export class PosCartState {
  readonly cart = signal<CartLine[]>([]);
  readonly customer = signal<Customer | null>(null);
  readonly cash = signal(0);
  readonly card = signal(0);
  readonly bonus = signal(0);
  readonly discountPercent = signal(0);
  readonly discountManual = signal(0);
  readonly discountByPercent = signal(true);
  readonly dueDate = signal('');

  resetPayments(): void {
    this.cash.set(0);
    this.card.set(0);
    this.bonus.set(0);
    this.discountPercent.set(0);
    this.discountManual.set(0);
    this.discountByPercent.set(true);
    this.dueDate.set('');
  }

  clearAll(): void {
    this.cart.set([]);
    this.customer.set(null);
    this.resetPayments();
  }
}

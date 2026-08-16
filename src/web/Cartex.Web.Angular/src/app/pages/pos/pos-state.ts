import { Injectable, signal } from '@angular/core';
import { Customer } from '../../core/models';

export interface CartLine {
  variantId: number;
  name: string;
  unitName: string;
  price: number;
  originalPrice: number;
  qty: number;
  available: number;
  allowsAmountEntry: boolean;
  allowsFractional: boolean;
}

export interface HeldSale {
  cart: CartLine[];
  customer: Customer | null;
  total: number;
  heldAt: string;
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
  readonly rounding = signal(0);
  readonly note = signal('');
  readonly dueDate = signal('');
  readonly held = signal<HeldSale[]>(JSON.parse(localStorage.getItem('cartex.heldSales') ?? '[]'));

  hold(): void {
    if (!this.cart().length) return;
    const total = this.cart().reduce((sum, l) => sum + l.price * l.qty, 0);
    this.held.update((list) => [
      ...list,
      { cart: this.cart(), customer: this.customer(), total, heldAt: new Date().toISOString() },
    ]);
    localStorage.setItem('cartex.heldSales', JSON.stringify(this.held()));
    this.clearAll();
  }

  resume(index: number): void {
    const item = this.held()[index];
    if (!item || this.cart().length) return;
    this.cart.set(item.cart);
    this.customer.set(item.customer);
    this.held.update((list) => list.filter((_, i) => i !== index));
    localStorage.setItem('cartex.heldSales', JSON.stringify(this.held()));
  }

  resetPayments(): void {
    this.cash.set(0);
    this.card.set(0);
    this.bonus.set(0);
    this.discountPercent.set(0);
    this.discountManual.set(0);
    this.discountByPercent.set(true);
    this.rounding.set(0);
    this.note.set('');
    this.dueDate.set('');
  }

  clearAll(): void {
    this.cart.set([]);
    this.customer.set(null);
    this.resetPayments();
  }
}

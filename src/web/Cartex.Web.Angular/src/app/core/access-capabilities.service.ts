import { Injectable, inject } from '@angular/core';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AccessCapabilitiesService {
  private readonly auth = inject(AuthService);

  canSell(): boolean {
    return this.auth.hasPermission('sales.create|sales.checkout');
  }

  canQueue(allowSaleQueue: boolean): boolean {
    return allowSaleQueue && this.auth.hasPermission('sales.pick|sales.view');
  }

  canUseCart(allowSaleQueue: boolean): boolean {
    return this.canSell() || this.canQueue(allowSaleQueue);
  }
}

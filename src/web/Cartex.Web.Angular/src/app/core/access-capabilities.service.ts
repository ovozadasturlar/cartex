import { Injectable, inject } from '@angular/core';
import { AuthService } from './auth.service';
import { FeaturesService } from './features.service';

@Injectable({ providedIn: 'root' })
export class AccessCapabilitiesService {
  private readonly auth = inject(AuthService);
  private readonly features = inject(FeaturesService);

  canSell(): boolean {
    return this.auth.hasPermission('sales.create|sales.checkout') && this.features.has('ordering|store');
  }

  canQueue(allowSaleQueue: boolean): boolean {
    return allowSaleQueue
      && this.auth.hasPermission('sales.pick|sales.view')
      && this.features.has('ordering|store');
  }

  canUseCart(allowSaleQueue: boolean): boolean {
    return this.canSell() || this.canQueue(allowSaleQueue);
  }
}

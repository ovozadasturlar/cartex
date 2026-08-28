import { Injectable, inject, signal } from '@angular/core';
import { lastValueFrom } from 'rxjs';
import { SalesPolicy, SettingsApi } from './api/settings.api';

/// BRAK-06: ba'zi bo'limlar ruxsatdan tashqari do'kon siyosatiga ham bog'liq. Siyosat bir marta
/// yuklanadi va menyu ham, marshrut qorovullari ham shu yagona nusxadan foydalanadi — aks holda
/// menyuda ko'rinmagan sahifa manzil orqali ochilib qolardi.
@Injectable({ providedIn: 'root' })
export class SalesPolicyService {
  private readonly api = inject(SettingsApi);
  private loading: Promise<void> | null = null;

  readonly policy = signal<SalesPolicy | null>(null);

  ensureLoaded(): Promise<void> {
    if (this.policy()) return Promise.resolve();
    return (this.loading ??= lastValueFrom(this.api.salesPolicy())
      .then((policy) => this.policy.set(policy))
      .catch(() => undefined)
      .finally(() => (this.loading = null)));
  }

  /// Modullardan farqli: siyosat kaliti standart holatda **yopiq**, shuning uchun hali
  /// noma'lum siyosat ham yopiq deb qaraladi. Siyosat kelgach menyu o'zi qayta hisoblanadi.
  has(key: keyof SalesPolicy | undefined): boolean {
    if (!key) return true;
    return this.policy()?.[key] === true;
  }

  reset(): void {
    this.policy.set(null);
  }
}

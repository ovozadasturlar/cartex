import { Injectable, inject, signal } from '@angular/core';
import { lastValueFrom } from 'rxjs';
import { FeaturesApi } from './api/misc.api';

/// RUXSAT-04: o'chirilgan modul interfeysda umuman ko'rinmasligi kerak — havola ham,
/// sahifaning o'zi ham. Shuning uchun yoqilgan modullar ro'yxati bir marta yuklanadi va
/// menyu ham, marshrut qorovullari ham shu yagona nusxadan foydalanadi.
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly api = inject(FeaturesApi);
  private loading: Promise<void> | null = null;

  readonly enabled = signal<ReadonlySet<string> | null>(null);

  ensureLoaded(): Promise<void> {
    if (this.enabled()) return Promise.resolve();
    return (this.loading ??= lastValueFrom(this.api.enabled())
      .then((codes) => this.enabled.set(new Set(codes)))
      .catch(() => undefined)
      .finally(() => (this.loading = null)));
  }

  /// Ro'yxat hali yuklanmagan bo'lsa modul yopiq deb qaralmaydi: aks holda sahifa birinchi
  /// ochilishida foydalanuvchi o'ziga tegishli bo'limdan haydab yuborilardi.
  has(expression: string | undefined): boolean {
    if (!expression) return true;
    const set = this.enabled();
    return !set || expression.split('|').some((code) => set.has(code));
  }

  reset(): void {
    this.enabled.set(null);
  }
}

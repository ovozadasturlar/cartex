import { Component, OnInit, inject, signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { FeaturesApi, OwnerModule } from '../../core/api/misc.api';
import { AuthService } from '../../core/auth.service';
import { FeaturesService } from '../../core/features.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

/// Ikki qatlamli kalitning egaga tegishli tarafi (SOZ-08): developer bo'limidagi ekran
/// do'kon nimani sotib olganini, bu yerdagisi do'kon nimani ishlatishini hal qiladi.
@Component({
  selector: 'app-modules',
  imports: [MatProgressBarModule, MatSlideToggleModule, TranslocoModule, PageHeader],
  templateUrl: './modules.html',
  styleUrl: './modules.scss',
})
export class Modules implements OnInit {
  private static readonly salesPolicyCodes = new Set(['multicurrency_pricing', 'multicurrency_sales']);
  private readonly api = inject(FeaturesApi);
  private readonly features = inject(FeaturesService);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly canEdit = inject(AuthService).hasPermission('business.edit');
  readonly loading = signal(true);
  readonly modules = signal<OwnerModule[]>([]);
  readonly multicurrencyLicensed = signal(false);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async toggle(module: OwnerModule): Promise<void> {
    if (!this.canEdit || !module.available) return;
    try {
      await lastValueFrom(this.api.setModule(module.code, !module.isEnabled));
      // RUXSAT-04a: bitta joyda o'zgargan imkoniyat butun ilovaga - menyu, qorovullar - darhol
      // yetib borishi kerak, aks holda to'liq qayta yuklashgacha eskirgan holatda qoladi.
      this.features.reset();
      await Promise.all([this.load(), this.features.ensureLoaded()]);
      this.notify.success(this.transloco.translate('success'));
    } catch (e) {
      this.notify.error(e);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const modules = await lastValueFrom(this.api.modules());
      this.modules.set(modules.filter((module) => !Modules.salesPolicyCodes.has(module.code)));
      // multicurrency (asosiy) modul ro'yxatida yo'q; litsenziyasi bolasidan (pricing) olinadi —
      // faol holatidan emas, aks holda ega kaliti o'chiq litsenziyalangan modul "Tarifingizda yo'q"
      // ko'rinardi (SOZ-08b).
      this.multicurrencyLicensed.set(
        modules.some((m) => m.code === 'multicurrency_pricing' && m.available),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}

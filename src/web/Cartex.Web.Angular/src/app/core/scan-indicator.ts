import { signal } from '@angular/core';

export type ScanIndicatorState = 'hidden' | 'busy' | 'found' | 'missing';

const SHOW_DELAY_MS = 150;
const RESULT_MS = 1400;

export class ScanIndicator {
  readonly state = signal<ScanIndicatorState>('hidden');

  private generation = 0;
  private timer?: ReturnType<typeof setTimeout>;

  async track<T>(lookup: Promise<T>, found?: (result: T) => boolean): Promise<T> {
    const generation = ++this.generation;
    clearTimeout(this.timer);
    this.state.set('hidden');

    const settled = lookup.then(() => false).catch(() => false);
    const delayed = new Promise<boolean>((resolve) => setTimeout(() => resolve(true), SHOW_DELAY_MS));
    const slow = await Promise.race([settled, delayed]);
    if (slow && generation === this.generation) this.state.set('busy');

    try {
      const result = await lookup;
      const hit = found ? found(result) : result !== null && result !== undefined;
      if (slow && generation === this.generation) this.show(hit ? 'found' : 'missing');
      return result;
    } catch (error) {
      if (generation === this.generation) this.hide();
      throw error;
    }
  }

  private show(state: ScanIndicatorState): void {
    this.state.set(state);
    this.timer = setTimeout(() => this.hide(), RESULT_MS);
  }

  private hide(): void {
    clearTimeout(this.timer);
    this.state.set('hidden');
  }
}

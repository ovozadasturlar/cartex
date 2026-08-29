import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { NotifyService } from '../../core/notify.service';

export async function downloadProductImage(url: string, name: string): Promise<void> {
  const response = await fetch(url);
  if (!response.ok) throw new Error(String(response.status));
  const objectUrl = URL.createObjectURL(await response.blob());
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = `${name.replace(/[\\/:*?"<>|]/g, '_') || 'product'}.png`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
}

@Component({
  selector: 'app-product-image-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, TranslocoModule],
  styleUrl: './products.scss',
  template: `
    <div class="image-viewer" *transloco="let t">
      <div class="image-viewer-head">
        <h2>{{ data.name }}</h2>
        <div>
          <button mat-stroked-button (click)="download()"><mat-icon>download</mat-icon>{{ t('download') }}</button>
          <button mat-icon-button (click)="ref.close()"><mat-icon>close</mat-icon></button>
        </div>
      </div>
      <div mat-dialog-content><img [src]="data.url" [alt]="data.name" /></div>
    </div>
  `,
})
export class ProductImageDialog {
  readonly data = inject<{ name: string; url: string }>(MAT_DIALOG_DATA);
  readonly ref = inject<MatDialogRef<ProductImageDialog>>(MatDialogRef);
  private readonly notify = inject(NotifyService);

  async download(): Promise<void> {
    try {
      await downloadProductImage(this.data.url, this.data.name);
    } catch (error) {
      this.notify.error(error);
    }
  }
}

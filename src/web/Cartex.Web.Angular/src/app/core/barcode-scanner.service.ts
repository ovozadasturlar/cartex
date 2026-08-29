import { Injectable } from '@angular/core';

export type ScannerBlock = 'none' | 'insecure' | 'unsupported';

interface DetectedBarcode {
  rawValue: string;
  format: string;
}

interface BarcodeDetectorLike {
  detect(source: CanvasImageSource): Promise<DetectedBarcode[]>;
}

interface BarcodeDetectorCtor {
  new (options?: { formats?: string[] }): BarcodeDetectorLike;
  getSupportedFormats?: () => Promise<string[]>;
}

/// Do'kon shtrixkodlari: savdo etiketkalari (EAN/UPC), ichki kodlar (Code 128/39, ITF) va
/// QR. Ro'yxat qisqa bo'lgani yaxshi — dvigatel qancha kam format qidirsa, shuncha tez va
/// aniq o'qiydi.
const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'itf', 'qr_code'];

/// Qaror brauzer holatidan ajratilgan sof funksiyada — har uch sabab test bilan qoplanadi.
export function scannerBlock(secure: boolean, hasCamera: boolean, hasDetector: boolean): ScannerBlock {
  if (!secure) return 'insecure';
  if (!hasCamera || !hasDetector) return 'unsupported';
  return 'none';
}

/// Skaner brauzerning **o'z** `BarcodeDetector` dvigatelida ishlaydi: Android Chrome'da u
/// Google ML Kit ustida turadi, ya'ni Cartex Do'kon ilovasidagi bilan bir xil o'qish sifati,
/// lekin qo'shimcha kutubxonasiz. Dvigatel yo'q brauzerda (mas. iOS Safari) kamera tugmasi
/// umuman ko'rsatilmaydi — qo'lda kiritish esa hamma joyda ishlayveradi.
@Injectable({ providedIn: 'root' })
export class BarcodeScannerService {
  /// Kamera faqat xavfsiz kontekstda (https yoki localhost) ochiladi — bu brauzer sharti,
  /// shuning uchun sabab alohida qaytariladi: foydalanuvchi "ishlamayapti" emas, "manzil
  /// https bo'lishi kerak" degan javobni ko'rsin.
  blockedBy(): ScannerBlock {
    return scannerBlock(
      window.isSecureContext,
      typeof navigator.mediaDevices?.getUserMedia === 'function',
      Boolean(this.detectorCtor()),
    );
  }

  get supported(): boolean {
    return this.blockedBy() === 'none';
  }

  create(): BarcodeDetectorLike | null {
    const ctor = this.detectorCtor();
    return ctor ? new ctor({ formats: FORMATS }) : null;
  }

  private detectorCtor(): BarcodeDetectorCtor | undefined {
    return (window as unknown as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector;
  }
}

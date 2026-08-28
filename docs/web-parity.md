# Desktop → Web paritet

> Web klient desktopning to'liq o'rnini bosishi kerak. Bu hujjat farqlarni qayd etadi va
> nima bajarilganini ko'rsatadi. Manba: `src/desktop/Cartex.UI/ViewModels/*ViewModel.cs`
> dagi `[RelayCommand]` lar va `src/web/Cartex.Web.Angular/src/app/pages/*` metodlari.

## Bajarilgan

| Yo'qolgan imkoniyat | Qayerda edi | Web'da endi |
|---|---|---|
| **Navbat signali** — `Subscribe(branchId)` chaqirilmasdi, `NAVBAT-08` dan keyin navbat umuman yangilanmasdi | `core/queue-hub.service.ts` | filialga obuna, `connectionId` bo'yicha kuzatuv, cheksiz qayta ulanish |
| **Chop etish natijasi** — hech qanday javob yo'q edi | — | `core/print-status-hub.service.ts` (toast) |
| **Chop etish holati** — printer oflayn bo'lsa ham "yuborildi" | `core/remote-print.service.ts` | job holati tekshiriladi, ogohlantirish chiqadi |
| **CSV eksport** — 15 ta ekranda yo'q edi | 15 sahifa | `core/csv-export.ts` + har ekranda tugma |
| **Mijoz boshlang'ich qoldig'i** — yaratishda 0 qattiq yozilgan, tahrirda umuman yo'q | `customers/customer-profile.ts` | `QARZ-24` bo'yicha yaratish va tuzatish |
| **`auto` xabar kanali** | `customers/send-message.dialog.ts` | ro'yxatda birinchi |
| **Defter yozuvidan amallar** — savdoni ochish, kvitansiyani chop etish, to'lovni bekor qilish | `customer-profile.html` defter jadvali | uch tugma, `customer_payments.void` ruxsati bilan |
| **Savdoni tuzatish** | `sales/sales.ts` chek oynasi | bekor qilish + savatni to'liq qaytarish |
| **Bo'sh narx** — jimgina tashlanardi | `pos/pos.ts` `onPrice` | 0 bo'ladi; 0 narxli qator savdoni to'xtatadi |
| **Mahsulot variantlari** — boshqaruv umuman yo'q edi | `products/` | `products/variants.dialog.ts` |
| **Qaytarish hujjatini chop etish** | `returns/return-detail.dialog.ts` | tarmoq printeriga yuboriladi |
| **Dialoglarda avtofokus** — 41 tadan 3 tasida | 12 ta shakl oynasi | `cdkFocusInitial` + `autoFocus: 'first-tabbable'` |

## Hali qolgan farqlar

Ustuvorlik tartibida. Har biri alohida ish sifatida olinadi.

### 1. Kundalik ish oqimi

| Desktop | Holat |
|---|---|
| Kirimga **Excel import** (`SuppliesViewModel.ImportExcelAsync`, `ImportTemplateAsync`) | web'da yo'q |
| Kirim qatorini **skanerlash** (`ScanLineAsync`) | web'da yo'q |
| Kirimdan **barkod chop etish** (`OpenPrintBarcode`) | web'da yo'q |
| Mahsulot **paketlari** (`EditPack`, `SavePackAsync`, `DeletePackAsync`) | web'da yo'q |
| Mahsulot qatoridan **barkod chop etish** | web'da yo'q |
| **Saqlab, yangisini boshlash** (`SaveAndNewAsync`) | web'da yo'q |

### 2. Sozlama va boshqaruv

| Desktop | Holat |
|---|---|
| **Onboarding** sehrgari (`OnboardingViewModel`) | web'da yo'q |
| **Ta'minotchi profili**: kengaytirilgan bo'limlar | web'da qisqartirilgan |
| **Chop etish**: yorliq kalibrovkasi, sinov chop etishlari (`CalibrateLabel`, `TestPrint`, `NudgeLabel*`) | web'da yo'q — bular qurilmaga xos, web'dan mantiqan bajarilmaydi |

### 3. Ataylab web'da bo'lmaydigan narsalar

Bular qurilmaga bog'liq va web'da ma'noga ega emas:

- lokal printer sozlamalari, yorliq kalibrovkasi, PDF papkasini tanlash;
- oflayn kesh va navbat (`SettingsViewModel` dagi oflayn boshqaruv);
- apparat kaliti (`HardwareKeys`) — web'da faqat ro'yxat ko'rinadi;
- HUB va qurilma orasidagi lokal aloqa.

## Tekshirish

```
cd src/web/Cartex.Web.Angular
npx ng build --configuration development
npx ng test --watch=false
```

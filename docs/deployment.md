# Cartex o'rnatish

## Lokal server (asosiy rejim — do'kon kompyuterida)

`deploy/installer/cartex.iss` (Inno Setup 6) bir-klik o'rnatuvchi yasaydi:
portable PostgreSQL 16 + Cartex API (Windows service, avto-start, restart-on-fail)
+ Desktop ilova. O'rnatishda developer parol so'raladi (production'da majburiy),
JWT kaliti va DB parol avtomatik generatsiya qilinadi, firewall'da 5015 port ochiladi.

Tayyorlash (release mashinasida):
```
dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/api
dotnet publish src/frontend/Cartex.Desktop/Cartex.Desktop.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/desktop
# PostgreSQL 16 Windows binaries zip -> deploy/installer/pgsql
iscc deploy/installer/cartex.iss
```

Boshqa kassalar (bir LAN'da): Desktop o'rnatiladi, Sozlamalar → Server manzili →
`http://<server-IP>:5015`. Server manzili restart TALAB QILMAYDI (jonli almashadi).

Tahdid modeli: LAN ishonchli perimetr — do'kon Wi-Fi'si WPA2+ va alohida SSID bo'lsin.
Internet o'chsa hech narsa to'xtamaydi (hammasi lokal). Telegram/SMS/Email xabarnomalari
internet qaytganda o'z-o'zidan davom etadi (outbox 5 marta qayta uradi).

## VPS (ixtiyoriy — masofaviy kirish)

```
cd deploy
cat > .env <<ENV
DB_PASSWORD=<kuchli-parol>
JWT_KEY=<64+ belgi tasodifiy>
DEVELOPER_PASSWORD=<developer parol>
DOMAIN=shop.example.uz   # faqat tls profili uchun
ENV
docker compose up -d              # HTTP 5015
docker compose --profile tls up -d  # + Caddy avto-HTTPS (PublicBaseUrl uchun)
```

`PublicBaseUrl` (Sozlamalar → Integratsiyalar) faqat tashqaridan ochiladigan HTTPS
manzil bo'lganda to'ldiriladi — shunda chek havolalari mijoz telefonida ochiladi.
Lokal-only o'rnatishda chek Telegram/Email orqali PDF fayl sifatida boradi.

## Eslatmalar
- Server vaqt zonasi to'g'ri bo'lsin — qarz eslatmalari `SendHourLocal` mahalliy soatga tayanadi.
- PostgreSQL major yangilashda `pg_upgrade` rejasi kerak (paket PG16 ga qadalgan).
- Sinov chop etish: Sozlamalar → Chop etish → etiketka o'lchami presetlari (58×40 default).

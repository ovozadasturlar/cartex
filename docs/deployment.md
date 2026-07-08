# Cartex o'rnatish

## Lokal server (asosiy rejim — do'kon kompyuterida)

`deploy/installer/cartex.iss` (Inno Setup 6) bir-klik o'rnatuvchi yasaydi:
portable PostgreSQL 16 + Cartex API (Windows service, avto-start, restart-on-fail)
+ Desktop ilova. O'rnatishda developer parol so'raladi (production'da majburiy),
JWT kaliti va DB parol avtomatik generatsiya qilinadi, firewall'da 5015 port ochiladi.

Tayyorlash (release mashinasida):
```
dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/api
dotnet publish src/desktop/Cartex.Desktop/Cartex.Desktop.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/desktop
# PostgreSQL 16 Windows binaries zip -> deploy/installer/pgsql
iscc deploy/installer/cartex.iss
```

Boshqa kassalar (bir LAN'da): Desktop o'rnatiladi, Sozlamalar → Server manzili →
`http://<server-IP>:5015`. Server manzili restart TALAB QILMAYDI (jonli almashadi).

Tahdid modeli: LAN ishonchli perimetr — do'kon Wi-Fi'si WPA2+ va alohida SSID bo'lsin.
Internet o'chsa hech narsa to'xtamaydi (hammasi lokal). Telegram/SMS/Email xabarnomalari
internet qaytganda o'z-o'zidan davom etadi (outbox eksponensial kutish bilan 12 martagacha qayta uradi — bir necha soatlik uzilishga chidaydi).

## VPS (ixtiyoriy — masofaviy kirish)

```
cd deploy
cat > .env <<ENV
DB_PASSWORD=<kuchli-parol>
JWT_KEY=<64+ belgi tasodifiy>
DEVELOPER_PASSWORD=<developer parol>
DOMAIN=shop.example.uz   # faqat tls profili uchun
BARCODE_PREFIX=XN        # har mijozga unikal (default CTX); generatsiya: XN-000042, XN-P6-000042
ENV
docker compose up -d              # HTTP 5015
docker compose --profile tls up -d  # + Caddy avto-HTTPS (PublicBaseUrl uchun)
```

`PublicBaseUrl` (Sozlamalar → Integratsiyalar) faqat tashqaridan ochiladigan HTTPS
manzil bo'lganda to'ldiriladi — shunda chek havolalari mijoz telefonida ochiladi.
Lokal-only o'rnatishda chek Telegram/Email orqali PDF fayl sifatida boradi.

## Chek-ko'zgu (lokal serverda ham ishlaydigan chek havolalari)

Server do'konda tursa-yu, chek havolalari dunyodan ochilishi kerak bo'lsa —
`Cartex.Mirror` mitti relayni istalgan VPS'ga qo'ying:

```
docker build -f src/backend/Cartex.Mirror/Dockerfile -t cartex-mirror .
docker run -d --name cartex-mirror -p 8090:8080 \
  -e Mirror__Keys=<litsenziya-kalit> \
  -v mirror-data:/data cartex-mirror
```

Ko'zgu bitta bo'lib bir nechta biznesga xizmat qiladi (`Mirror__Keys` vergul bilan
bir nechta kalit oladi). Do'kon tomonida Sozlamalar → Integratsiyalar → Bulut ko'prigi:
Enabled + GatewayUrl (masalan `https://mirror.example.uz`) + litsenziya kaliti;
`PublicBaseUrl` ham shu manzilga qo'yiladi. Har savdodan keyin chek HTML+PDF outbox
orqali ko'zguga boradi (internet uzuq bo'lsa navbatda kutadi), mijoz havolani istalgan
joydan ochadi.

## Eslatmalar
- Server vaqt zonasi to'g'ri bo'lsin — qarz eslatmalari `SendHourLocal` mahalliy soatga tayanadi.
- PostgreSQL major yangilashda `pg_upgrade` rejasi kerak (paket PG16 ga qadalgan).
- Sinov chop etish: Sozlamalar → Chop etish → etiketka o'lchami presetlari (58×40 default).

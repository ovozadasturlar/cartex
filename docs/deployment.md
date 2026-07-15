# Cartex o'rnatish

Ikki ssenariy bor. Ikkalasida ham **zaxira majburiy** — pastdagi "Zaxira" bo'limiga qarang.

| | Lokal (do'kon kompyuteri) | Server (VPS / do'kon serveri) |
|---|---|---|
| O'rnatish | Inno Setup `.exe` | `docker compose` |
| Baza | Portable PostgreSQL 16 (Windows servis) | `postgres:16-alpine` konteyneri |
| Rasm saqlash | Lokal disk (`C:\ProgramData\Cartex\storage`) | Lokal disk (volume) yoki MinIO |
| Web UI | Yo'q (faqat Desktop + mobil) | Bor — `http://<server>:5015` |
| Internet | Kerak emas | Kerak |

---

## 1. Lokal server (do'kon kompyuterida)

`deploy/installer/cartex.iss` (Inno Setup 6) bir-klik o'rnatuvchi yasaydi:
portable PostgreSQL 16 + Cartex API (Windows servis, avto-start, restart-on-fail) + Desktop ilova.
O'rnatishda developer va admin paroli so'raladi; JWT kaliti va DB paroli **kriptografik tasodifiy**
generatsiya qilinadi; firewall'da 5015 port ochiladi.

Tayyorlash (release mashinasida):
```
dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/api
dotnet publish src/desktop/Cartex.Desktop/Cartex.Desktop.csproj -c Release -r win-x64 --self-contained -o deploy/installer/publish/desktop
# PostgreSQL 16 Windows binaries zip -> deploy/installer/pgsql
iscc deploy/installer/cartex.iss
```

**Ma'lumot joylashuvi** (zaxira uchun muhim):

| Nima | Qayerda |
|---|---|
| Baza | `C:\Program Files\Cartex\pgdata` (PostgreSQL servisi `CartexDb`) |
| Rasmlar | `C:\ProgramData\Cartex\storage` |
| Shifrlash kalitlari | `C:\ProgramData\Cartex\keys` |
| Sozlamalar (sirlar) | `C:\Program Files\Cartex\api\appsettings.Production.json` |

Qayta o'rnatishda mavjud `pgdata` va `appsettings.Production.json` **saqlanadi** (kalitlar o'zgarmaydi).
O'chirishda (uninstall) servislar va firewall qoidasi tozalanadi, lekin baza va rasmlar **qoladi**.

Baza `scram-sha-256` bilan himoyalangan (parolsiz `trust` emas), `appsettings.Production.json`
esa faqat SYSTEM va Administrators uchun o'qiladi — kompyuterdagi boshqa foydalanuvchi
JWT kalitini yoki baza parolini ko'ra olmaydi.

Boshqa kassalar (bir LAN'da): Desktop o'rnatiladi, Sozlamalar → Server manzili →
`http://<server-IP>:5015`. Server manzili restart TALAB QILMAYDI (jonli almashadi).

Tahdid modeli: LAN ishonchli perimetr — do'kon Wi-Fi'si WPA2+ va alohida SSID bo'lsin.
Internet o'chsa hech narsa to'xtamaydi (hammasi lokal). Telegram/SMS/Email xabarnomalari
internet qaytganda o'z-o'zidan davom etadi (outbox eksponensial kutish bilan 12 martagacha qayta uradi).

---

## 2. Server (Docker)

```
cd deploy
cp .env.example .env     # keyin qiymatlarni to'ldiring (4 ta parol majburiy)
docker compose up -d
```

Ochiladi: **`http://<server>:5015`** — shu manzilda ham API, ham Angular web UI turadi
(Desktop ilova ham shu manzilga ulanadi).

Profillar:
```
docker compose --profile tls up -d      # + Caddy avto-HTTPS (DOMAIN kerak)
docker compose --profile minio up -d    # + MinIO obyekt saqlash (MINIO_USER/PASSWORD kerak)
```

MinIO yoqilgach: Sozlamalar → Integratsiyalar → Saqlash → `minio`, Endpoint `minio:9000`,
Bucket nomi, Access/Secret kalit.

MinIO portlari **faqat serverning o'zidan** ochiladi (`127.0.0.1:9000`, `127.0.0.1:9001`) —
rasmlarni brauzer emas, API o'zi ichki tarmoq orqali oladi. Konsolga kirish uchun SSH tunnel:
`ssh -L 9001:127.0.0.1:9001 user@server`, keyin `http://localhost:9001`.
**Diqqat:** provayderni almashtirsangiz eski rasmlar yangi joyda yo'q — ular ko'chirilmaydi.
Shuning uchun saqlash turini **birinchi kundan** tanlang.

Volume'lar (ma'lumot shu yerda — konteyner o'chsa ham qoladi):

| Volume | Nima |
|---|---|
| `cartex-db` | PostgreSQL bazasi |
| `cartex-storage` | Rasmlar (lokal rejim) |
| `cartex-keys` | DataProtection kalitlari (shifrlangan sirlarni ochish uchun) |
| `cartex-minio` | MinIO obyektlari (minio profili) |

`PublicBaseUrl` (Sozlamalar → Integratsiyalar) faqat tashqaridan ochiladigan HTTPS
manzil bo'lganda to'ldiriladi — shunda chek havolalari mijoz telefonida ochiladi.

---

## 3. Zaxira (MAJBURIY — ikkala ssenariyda ham)

Zaxiraga **ikki narsa** kirishi shart, aks holda tiklash to'liq bo'lmaydi:

1. **PostgreSQL bazasi** (`cartex`) — savdo, mahsulot, mijoz, pul — hammasi shu yerda.
2. **Rasm saqlagichi** — lokal rejimda `storage` papkasi/volume, MinIO rejimida MinIO bucket'i.

Qo'shimcha: `keys` papkasi/volume (DataProtection) — busiz shifrlangan integratsiya sirlari
(MinIO secret, SMTP paroli) tiklanmaydi. Kichik, lekin zaxiraga qo'shing.

Zaxira uchun **Zaxira platformasi** (`muqimjon/zaxira` agent) ishlatiladi — u Docker soketi
orqali Postgres va MinIO'ni o'zi topadi (`/var/run/docker.sock:ro`) va jadval bo'yicha
hub'ga yuboradi. Agent compose bloki Zaxira repo'sida.

> **Lokal (Windows, Dockersiz) o'rnatishda** Zaxira agenti bash+Docker'ga tayanadi va native
> ishlamaydi. Ikki yo'l bor: (a) do'kon kompyuteriga ham Docker Desktop qo'yish, (b) Windows
> uchun rejalashtirilgan vazifa (`pg_dump` + `storage` papkasi + rclone yuklash). Bu qaror
> hali yopilmagan.

---

## Eslatmalar
- Server vaqt zonasi to'g'ri bo'lsin — qarz eslatmalari `SendHourLocal` mahalliy soatga tayanadi.
- PostgreSQL major yangilashda `pg_upgrade` rejasi kerak (paket PG16 ga qadalgan).
- Barkod prefiksi har mijozda unikal: `BARCODE_PREFIX=XN` (default `CTX`) → `XN-000042`, pachka: `XN-P6-000042`.
- Sinov chop etish: Sozlamalar → Chop etish → etiketka o'lchami presetlari (58×40 default).

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
`PublicBaseUrl` ham shu manzilga qo'yiladi.

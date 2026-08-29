# Cartex o'rnatish

Ikki ssenariy bor. Ikkalasida ham **zaxira majburiy** — pastdagi "Zaxira" bo'limiga qarang.

| | Lokal (do'kon kompyuteri) | Server (VPS / do'kon serveri) |
|---|---|---|
| O'rnatish | Inno Setup `.exe` | `docker compose` |
| Baza | Portable PostgreSQL (Windows servis) | `postgres:18-alpine` konteyneri |
| Rasm saqlash | Lokal disk (`C:\ProgramData\Cartex\storage`) | Lokal disk (volume) yoki MinIO |
| Web UI | Yo'q (faqat Desktop + mobil) | Bor — `http://<server>:8080` yoki `https://DOMAIN` |
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

## 2. Faqat Desktop mijozini o'rnatish

Bu paket API, PostgreSQL yoki servislarni o'rnatmaydi. U do'kondagi mavjud lokal serverga yoki
VPS'dagi API'ga ulanadigan qo'shimcha kassa kompyuterlari uchun mo'ljallangan.

Release yaratish:

```powershell
.\deploy\installer\build-desktop-client.ps1
```

Skript Desktop'ni `win-x64`, self-contained holatda publish qiladi va Inno Setup orqali quyidagi
faylni yaratadi:

```
deploy\installer\output\cartex-desktop-setup-0.0.1.exe
```

Mijoz kompyuterida faqat shu `.exe`ni ishga tushiring. Birinchi ishga tushgach,
Sozlamalar → Server manzili orqali API manzilini kiriting: lokal tarmoq uchun
`http://<server-IP>:5015`, VPS uchun esa HTTPS manzil. Inno Setup 6 build kompyuterida
o'rnatilgan va `ISCC.exe` PATH'da bo'lishi kerak.

> Installer bir nechta faylni bitta `setup.exe` ichiga joylaydi. Bu .NET'ning single-file
> publishidan ishonchliroq: yangilanish, native kutubxonalar va fayl resurslari bir xil ishlaydi.

---

## 3. Server (Docker)

Ikki image: **`muqimjon/cartex-api`** (API, port 5015) va **`muqimjon/cartex-web`**
(nginx + Angular; `/api`, `/hubs`, `/health`ni API'ga proxy qiladi).

```
cd deploy
cp .env.example .env     # keyin qiymatlarni to'ldiring (4 ta parol majburiy)
docker compose up -d
```

Ochiladi: web UI — **`http://<server>:8080`** (`WEB_PORT` bilan o'zgaradi), API —
`http://<server>:5015`. Desktop ilova LAN'da `http://<server>:5015`ga, internet orqali
esa `https://DOMAIN`ga ulanadi (nginx proxy orqali).

Profillar (`tls` va `traefik` bir vaqtda ishlatilmasin — ikkalasi ham 80/443 portini so'raydi,
qolganlari birga ishlatish mumkin):
```
docker compose --profile tls up -d      # + Caddy avto-HTTPS: DOMAIN -> web (DOMAIN kerak)
docker compose --profile traefik up -d  # + Traefik HTTPS va /pgadmin (bir nechta domen/servis bo'lsa)
docker compose --profile minio up -d    # + MinIO obyekt saqlash (MINIO_USER/PASSWORD kerak)
docker compose --profile backup up -d   # + Zaxira agenti (pastdagi bo'lim)
```

`traefik` profili: yagona domen/backend uchun odatda `tls` (Caddy) yetarli va soddaroq;
bir nechta loyiha/domenni bitta serverda markazlashtirib boshqarish kerak bo'lsa `traefik`
tanlansin (`DOMAIN`, `ACME_EMAIL` kerak). Shu profil bilan `https://DOMAIN/pgadmin` orqali
pgAdmin ham ko'tariladi (`PGADMIN_EMAIL`/`PGADMIN_PASSWORD` kerak) — bazani brauzerdan
boshqarish uchun.

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

## 4. Zaxira (MAJBURIY — ikkala ssenariyda ham)

Zaxiraga **ikki narsa** kirishi shart, aks holda tiklash to'liq bo'lmaydi:

1. **PostgreSQL bazasi** (`cartex`) — savdo, mahsulot, mijoz, pul — hammasi shu yerda.
2. **Rasm saqlagichi** — lokal rejimda `storage` papkasi/volume, MinIO rejimida MinIO bucket'i.

Qo'shimcha: `keys` papkasi/volume (DataProtection) — busiz shifrlangan integratsiya sirlari
(MinIO secret, SMTP paroli) tiklanmaydi. Kichik, lekin zaxiraga qo'shing.

Zaxira uchun **Zaxira platformasi** (`muqimjon/zaxira` agent) — `deploy/docker-compose.yml`da
tayyor `backup` profili bor: Postgres + MinIO **bitta izchil nuqtai-vaqt versiyasi** sifatida
zaxiralanadi va rclone orqali bulutga yuboriladi. Yoqish:

1. `.env`da: `BACKUP_MINIO_ENDPOINT=http://minio:9000`, `BACKUP_MINIO_BUCKET`,
   `BACKUP_RCLONE_REMOTE`, `BACKUP_RCLONE_PATH` (jadval: `BACKUP_SCHEDULE`, default 03:00).
2. `cp rclone.conf.example rclone.conf` — o'z bulut remote'ingizni joylang.
3. `docker compose --profile backup up -d`
4. Ixtiyoriy: `ZAXIRA_HUB_URL`/`ZAXIRA_HUB_TOKEN` — Hub'dan boshqarish uchun.

Deploydan keyin 1 kun ichida bulutda arxiv paydo bo'lganini tekshiring va **bitta restore
sinovi** o'tkazing — tiklanmaydigan zaxira zaxira emas.

> **Lokal (Windows, Dockersiz) o'rnatishda** Zaxira agenti bash+Docker'ga tayanadi va native
> ishlamaydi. Ikki yo'l bor: (a) do'kon kompyuteriga ham Docker Desktop qo'yish, (b) Windows
> uchun rejalashtirilgan vazifa (`pg_dump` + `storage` papkasi + rclone yuklash). Bu qaror
> hali yopilmagan.

---

## Eslatmalar
- Server vaqt zonasi to'g'ri bo'lsin — qarz eslatmalari `SendHourLocal` mahalliy soatga tayanadi.
- PostgreSQL major yangilashda `pg_upgrade` rejasi kerak (paket PG18 ga qadalgan; PG18'da volume `/var/lib/postgresql`ga ulanadi).
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

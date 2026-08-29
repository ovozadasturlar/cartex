# Cartex — yangi do'konga o'rnatish (LAN server)

Bitta kompyuter server bo'ladi: unda ma'lumotlar bazasi, backend, web-panel va
**barcha fayllar (mahsulot suratlari, logotip)** saqlanadi. Kassalar shu
kompyuterga lokal tarmoq orqali ulanadi — internet va domen shart emas.

## Talablar

- Windows'da Docker Desktop (yoki Linux'da docker + compose plugin)
- Server kompyuterga statik lokal IP bering (masalan `192.168.1.10`) — router
  sozlamasida DHCP reservation qilib qo'ying, aks holda IP o'zgarib kassalar uzilib qoladi.

## O'rnatish

```bash
# 1. Shu papkani (deploy/lan) server kompyuterga ko'chiring
# 2. .env yarating va to'ldiring
copy .env.example .env      # Linux/Mac: cp .env.example .env

# 3. Ishga tushiring
docker compose up -d

# 4. Tekshiring
curl http://localhost:5015/health        # -> Healthy
```

Birinchi ishga tushishda baza o'zi yaratiladi (migratsiyalar avtomatik) va
`developer` / `admin` foydalanuvchilari `.env` dagi parollar bilan ochiladi.

## Kirish manzillari

| Nima | Manzil |
|---|---|
| Kassa (desktop ilova, server manzili) | `http://<server-ip>:5015` |
| Web-panel (brauzer) | `http://<server-ip>:8080` |
| pgAdmin (faqat server kompyuterida) | `http://localhost:5050` |

Desktop ilovada birinchi ochilishda server manzili so'raladi — `http://<server-ip>:5015` kiriting.
Windows olov devori so'rasa, Docker uchun lokal tarmoqqa ruxsat bering
(yoki 5015 va 8080 portlarga inbound qoida qo'shing).

## Fayllar qayerda saqlanadi?

Lokal rejim (standart): suratlar va hujjatlar backend yonidagi `cartex-storage`
volume'ida — ya'ni **server qilib tanlangan kompyuterning o'zida**. MinIO shart
emas; u faqat fayllarni S3 uslubida alohida xizmatda saqlash kerak bo'lsa
(`docker compose --profile minio up -d`) ishlatiladi va keyin dastur ichidan
(Sozlamalar → Integratsiyalar → Saqlash) ulanadi. Kichik do'kon uchun tavsiya:
**lokal rejimda qoldiring** — kam qism, oson zaxira.

## Zaxira (majburiy odat)

Ma'lumot ikki joyda: baza (savdolar) va fayllar (suratlar). Ikkalasini birga oling:

```bash
docker compose exec db pg_dump -U postgres -Fc cartex > cartex-$(date +%F).dump
docker run --rm -v lan_cartex-storage:/data -v %cd%:/backup alpine tar czf /backup/storage-backup.tgz -C /data .
```

Zaxira faylini boshqa qurilmaga (flesh/boshqa kompyuter) ko'chirib qo'ying.

## Zaxiradan tiklash (boshqa qurilmada / bazani almashtirish)

Baza foydalanuvchisi standart `postgres` bo'lib, standart `postgres` bazasiga ulanadi —
shu sabab `cartex` ilova bazasini undan ulanib turib erkin DROP/CREATE qilish mumkin
(Postgres o'zi ulangan turgan bazani drop qilishga yo'l qo'ymaydi, shuning uchun bu ataylab shunday).

```bash
# 1. Zaxira faylini konteynerga nusxalang
docker cp cartex-2026-08-25.dump <loyiha>-db-1:/tmp/restore.dump

# 2. Ilova bazasini almashtiring (postgres bazasiga ulangan holda)
docker compose exec db psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS cartex;"
docker compose exec db psql -U postgres -d postgres -c "CREATE DATABASE cartex OWNER postgres;"
docker compose exec db pg_restore -U postgres -d cartex --no-owner --no-privileges /tmp/restore.dump

# 3. API'ni qayta ishga tushiring — u avtomatik ravishda kerakli migratsiyalarni qo'llaydi
docker compose restart api
```

## Yangilash

```bash
# .env ichida CARTEX_VERSION ni yangi raqamga ko'taring, keyin:
docker compose pull
docker compose up -d
```

Baza va fayllar volume'larda qoladi — yangilash ularni o'chirmaydi.
`docker compose down -v` HECH QACHON ishlatilmasin (`-v` hamma ma'lumotni o'chiradi).

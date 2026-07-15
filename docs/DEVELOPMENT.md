# Ishga tushirish (dasturchi uchun)

Repo'da **hech qanday sir yo'q** — ular ataylab commit qilinmagan. Shuning uchun repo'ni
klon qilgandan keyin har bir dasturchi o'zining lokal sirlarini bir marta o'rnatadi.

## Kerak bo'ladi

| Nima | Versiya |
|---|---|
| .NET SDK | 10.0 |
| PostgreSQL | 16+ (lokal yoki Docker) |
| Node.js | 22+ (faqat web uchun) |
| Docker | Testlar uchun (Testcontainers) |

## 1. Baza

Lokal PostgreSQL'da bo'sh baza yarating (migratsiya va seed avtomatik bajariladi):

```sql
CREATE DATABASE cartex;
```

yoki Docker bilan:

```
docker run -d --name cartex-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=cartex -p 5432:5432 postgres:16-alpine
```

## 2. Sirlar (user-secrets)

Ikki qiymat kerak. Ular `appsettings.json`da **yo'q** va bo'lmasligi ham kerak.

```
cd src/backend/Cartex.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=cartex;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:Key" "<kamida-32-belgi-tasodifiy-satr>"
```

`Jwt:Key` 32 baytdan qisqa bo'lsa API ishga tushmaydi. Kalit yaratish:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

## 3. Ishga tushirish

```
dotnet run --project src/backend/Cartex.Api          # http://localhost:5015
dotnet run --project src/desktop/Cartex.Desktop      # Avalonia
cd src/web/Cartex.Web.Angular && npm install && npm start   # http://localhost:4200
```

Birinchi ishga tushishda baza migratsiya qilinadi va demo ma'lumot bilan to'ldiriladi.

**Dev loginlar:** `admin/admin123`, `cashier/cashier123`, `picker/picker123`, `developer/developer123`.

## 4. Testlar

```
dotnet test          # 4 to'plam: Architecture, Unit, Application, Api.Integration
```

Application va Integration testlari **Docker talab qiladi** (Testcontainers PostgreSQL'ni
o'zi ko'taradi). Docker ishlamasa ular yiqiladi.

## Bilib qo'yish kerak

- **i18n yagona manbasi** — `src/desktop/Cartex.UI/Assets/Languages/*.json` (4 til).
  Web'dagi `public/i18n` **generatsiya qilinadi** (`npm start`/`npm run build` avtomatik ko'chiradi,
  git'da yo'q). Mobil ilovalarning o'z `Resources/Raw/i18n` fayllari bor.
  Til fayllarida kalit yetishmasa `LocalizationParityTests` yiqiladi.
- **Har endpoint himoyalangan** bo'lishi shart (`[HasPermission]` yoki whitelist) —
  `EndpointAuthorizationTests` shuni tekshiradi.
- Migratsiya: `dotnet ef migrations add <Nom> -p src/backend/Cartex.Persistence -s src/backend/Cartex.Api`

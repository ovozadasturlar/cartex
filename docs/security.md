# Xavfsizlik qoidalari

## Endpoint himoyasi
Har controller action `[HasPermission]` bilan himoyalangan — bu `EndpointAuthorizationTests`
arch-testi bilan majburlanadi. Istisnolar (oq-ro'yxat, testda kod sifatida saqlanadi):
- **Anonim**: `POST /api/auth/login`, `POST /api/auth/login-with-key`, `GET /r/{token}` (public chek), `GET /health`.
- **Faqat autentifikatsiya** (permissionsiz): `GET /api/business` (onboarding tekshiruvi va biznes nomi
  barcha rollarga kerak), `GET /api/expense-categories` (kassadagi chiqim oqimida sotuvchi ishlatadi).

Barcha controller'lar `[Authorize]` + FallbackPolicy (RequireAuthenticatedUser) ostida — atributsiz
action ham default yopiq.

## Tarif fail-closed
Noma'lum/buzuq tarif satri → Free tarif to'plami (hech qachon "hammasi ochiq" emas).

## Developer parol
Production muhitida `Seed:DeveloperPassword` MAJBURIY — berilmasa dastur ishga tushmaydi.
Mavjud bazada default parol aniqlansa avtomatik yangi parolga almashtiriladi.

## Transport
LAN ichida HTTP (Kestrel `Urls` config, default `http://localhost:5015`; o'rnatishda
`http://0.0.0.0:5015`). Tahdid modeli: LAN ishonchli perimetr — alohida Wi-Fi SSID (WPA2+) tavsiya
etiladi, JWT muddati 8 soat. TASHQI kirish faqat TLS-terminatsiyali kanal orqali: Caddy
reverse-proxy (avto Let's Encrypt) yoki Cloudflare Tunnel. `PublicBaseUrl` doim `https://`
bo'lishi kerak. Self-signed sertifikat ishlatilmaydi (har kassada trust boshqaruvi + mijoz
brauzerida ogohlantirish — foydadan ko'ra zarar).

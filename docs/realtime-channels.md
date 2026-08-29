# Real vaqt kanallari (SignalR)

> Bu hujjat **texnik qo'llanma**. Biznes qoidalari `docs/domain-rules.md` da:
> `CHOP-06`, `CHOP-10`, `CHOP-11`, `SMS-33`, `SMS-34`, `NAVBAT-08`.

Qurilmalar orasidagi avtomatik jarayonlar (chop etish, SMS, navbat) bitta mexanizm ustida
ishlaydi. Yangi kanal qo'shish uchun mexanizmni qayta qurish shart emas — quyidagi bo'laklar
tayyor, ular ustiga faqat o'z mantiqingiz yoziladi.

## Tayyor bo'laklar

| Bo'lak | Qayerda | Nima beradi |
|---|---|---|
| `HubChannels` | `Application/Common/Interfaces/IHubPresence.cs` | Kanal nomlarining yagona manbai. Nom to'qnashuvi bo'lmaydi. |
| `IHubPresence` | o'sha yerda | "Bu kanalni kimdir tinglayaptimi?" — marshrutlash shu savolga tayanadi, vaqt belgisiga emas. |
| `HubPresence` | `Api/Hubs/HubPresence.cs` | Yagona implementatsiya: ulanish → kanal hisobi. |
| `PresenceHub` | `Api/Hubs/PresenceHub.cs` | Hub asosi. `JoinAsync`/`LeaveAsync` guruh a'zoligi va presence'ni **birga** o'zgartiradi; uzilish avtomatik ishlanadi. |
| `HubConnections` | `Cartex.UI/Services` | Desktop klienti: token yangilash, qurilma sarlavhalari, cheksiz qayta ulanish. |
| `MobileHubConnections` | `Cartex.Mobile.Store/Services` | Telefon uchun o'shaning o'zi. |
| `HubSubscription` | ikkala klientda | Obunani ulanish identifikatoriga bog'laydi. "Ulangan, lekin obuna yo'q" holati strukturaviy ravishda yo'q. |

## Yangi kanal qo'shish tartibi

1. **Kanal nomi** — `HubChannels` ga bitta metod qo'shiladi.
2. **Hub** — `PresenceHub` dan meros olinadi. `Subscribe(...)` da ruxsat va identifikat
   tekshiriladi, so'ng `JoinAsync(HubChannels.Xxx(...))`. Uzilishni yozish **kerak emas**;
   qurilmaga xos yakuniy ish bo'lsa `OnChannelsLeftAsync` override qilinadi.
3. **Port** — `Application` da `IXxxNotifier` interfeysi, `Api` da SignalR implementatsiyasi
   (`hub.Clients.Group(HubChannels.Xxx(...))`). `Clients.All` **ishlatilmaydi** — signal doim
   manzilli bo'ladi.
4. **Marshrutlash** — ishni faqat `presence.IsOnline(HubChannels.Xxx(...))` bo'lgan qurilmaga
   tayinlang. `LastSeenAt` kabi vaqt oynalari qo'llanmaydi.
5. **Klient** — ulanish `HubConnections.Create(path, auth)` dan olinadi, obuna
   `HubSubscription.EnsureAsync(connection, subscribe)` orqali yangilanadi. `EnsureAsync`
   `true` qaytarsa kutayotgan ishni darhol olib keting. `Reconnected` va `Closed` da
   `Invalidate()` chaqiriladi.
6. **Zaxira** — klientda davriy so'rov yozilmaydi. Yo'qolgan ish serverdagi tiklash sikliga
   qoldiriladi (chop etish: 10 s, SMS: 30 s). Istisno faqat platforma majbur qilganda bo'ladi
   va sababi hujjatda yoziladi (`SMS-33` — Android uxlash rejimi).
7. **Qoida va test** — `docs/domain-rules.md` ga qoida yoziladi, test o'sha qoidadan yoziladi.

## Ataylab qilinmagan narsalar

- **Umumiy "ish navbati" abstraksiyasi yo'q.** Chop etish va SMS'da lease/qayta tiklash
  o'xshash, lekin shartlari boshqacha (kvota, tinch soatlar, sodiq SIM ↔ imkoniyat, nusxa,
  idempotency). Ikkita misoldan umumiy asos yasash erta: uchinchi haqiqiy kanal paydo
  bo'lganda shakl o'zidan ko'rinadi.
- **Notifier interfeyslari birlashtirilmagan.** Har feature o'z portiga ega bo'lishi
  Clean Architecture talabi; bitta `Send(channel, method, payload)` chaqiruvchilarda
  tip xavfsizligini yo'qotardi.
- **SignalR backplane (Redis) yo'q.** Presence va guruhlar API jarayonining xotirasida.
  Shuning uchun API **bitta nusxada** ishlaydi (`CHOP-11`). Ko'p nusxa kerak bo'lganda
  backplane va umumiy presence birga qo'shiladi — undan oldin bu ortiqcha qatlam.

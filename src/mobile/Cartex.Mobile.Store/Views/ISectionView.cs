namespace Cartex.Mobile.Store.Views;

/// Asosiy bo'limlar sahifa emas, ko'rinish: ular bir marta yaratiladi va `MainPage` ichida
/// qoladi. Shell tab almashganda butun fragmentni qayta yaratadi va ekran bir kadr yo'qoladi —
/// bu yerda esa faqat ko'rinuvchanlik almashadi, shuning uchun panel ham, kontent ham
/// hech qachon qayta chizilmaydi.
public interface ISectionView
{
    void Appear();

    void Disappear();

    /// Bo'lim ochiq overlay tufayli orqaga tugmasini o'zi yutadimi.
    bool HandleBack() => false;
}

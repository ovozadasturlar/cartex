using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Services;

// Tizimdan chiqish va serverni almashtirish bir xil tozalashni talab qiladi: eski do'kon
// savati, ombori va hub ulanishi keyingi sessiyaga o'tib ketmasligi kerak.
public sealed class StoreSignOut(
    MobileAuthService auth,
    OrderingHubService orderingHub,
    CartStore cart,
    SupplyCartStore supplyCart,
    WarehouseContext warehouse,
    HubLinkService hubLink)
{
    public async Task RunAsync()
    {
        AppLock.Disable();
        await auth.LogoutAsync();
        await orderingHub.StopAsync();
        cart.Clear();
        supplyCart.Clear();
        warehouse.Reset();
        // HUB-04: guvohnoma do'konga bog'langan — boshqa do'konga o'tilganda u yashab qolmasin.
        hubLink.Forget();
    }
}

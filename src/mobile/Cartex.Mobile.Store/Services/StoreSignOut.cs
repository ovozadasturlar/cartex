using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Services;

// Tizimdan chiqish va serverni almashtirish bir xil tozalashni talab qiladi: eski do'kon
// savati, ombori va hub ulanishi keyingi sessiyaga o'tib ketmasligi kerak.
public sealed class StoreSignOut(
    MobileAuthService auth,
    AccessState access,
    OrderingHubService orderingHub,
    CartStore cart,
    SupplyCartStore supplyCart,
    WarehouseContext warehouse,
    HubLinkService hubLink,
    MobileHubHostService hubHost,
    SmsGatewayHostService smsGateway)
{
    public async Task RunAsync()
    {
        AppLock.Disable();
        await auth.LogoutAsync();
        access.Clear();
        await orderingHub.StopAsync();
        await smsGateway.StopAsync();
        await hubHost.ApplyAsync();
        cart.Clear();
        supplyCart.Clear();
        warehouse.Reset();
        // HUB-04: guvohnoma do'konga bog'langan — boshqa do'konga o'tilganda u yashab qolmasin.
        hubLink.Forget();
    }
}

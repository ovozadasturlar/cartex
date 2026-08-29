using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Warehouses;

namespace Cartex.Mobile.Store.Services;

public sealed class WarehouseContext(IWarehousesApi warehousesApi, MobileAuthService auth, AccessState access)
{
    private string Key => $"store_wh_{auth.UserId}";

    public long? WarehouseId
    {
        get
        {
            var id = Preferences.Get(Key, 0L);
            return id == 0 ? null : id;
        }
    }

    public string WarehouseName => Preferences.Get(Key + "_name", "");

    public async Task<bool> EnsureSelectedAsync()
    {
        if (WarehouseId is not null) return true;
        var candidates = await CandidatesAsync();
        if (candidates.Count == 1)
        {
            Set(candidates[0]);
            return true;
        }
        return await PickAsync(candidates);
    }

    public async Task ChangeAsync()
    {
        var before = WarehouseId;
        await PickAsync(await CandidatesAsync());
        if (WarehouseId != before)
            await access.RefreshAsync();
    }

    public void Reset()
    {
        Preferences.Remove(Key);
        Preferences.Remove(Key + "_name");
    }

    private async Task<List<WarehouseDto>> CandidatesAsync()
    {
        var all = await warehousesApi.GetAllAsync();
        var branchId = auth.DefaultBranchId;
        var own = all.Where(w => w.AssignedUserId is null || w.AssignedUserId == auth.UserId).ToList();
        var inBranch = branchId is null ? own : own.Where(w => w.BranchId == branchId).ToList();
        return inBranch.Count > 0 ? inBranch : own;
    }

    private async Task<bool> PickAsync(List<WarehouseDto> candidates)
    {
        if (candidates.Count == 0) return false;
        var names = candidates.Select(w => w.Name).ToArray();
        var page = Shell.Current?.CurrentPage;
        if (page is null) return false;
        var choice = await page.DisplayActionSheetAsync(Loc.Instance["warehouse_pick"], Loc.Instance["cancel"], null, names);
        var picked = candidates.FirstOrDefault(w => w.Name == choice);
        if (picked is null) return WarehouseId is not null;
        Set(picked);
        return true;
    }

    // HUB-10: yo'ldosh rejimida ombor tanlanmaydi — u HUB'niki bo'ladi, chunki ekrandagi
    // qoldiq ham, narx ham o'sha ombordan kelgan.
    public void Force(long id, string name)
    {
        Preferences.Set(Key, id);
        Preferences.Set(Key + "_name", name);
    }

    private void Set(WarehouseDto warehouse) => Force(warehouse.Id, warehouse.Name);
}

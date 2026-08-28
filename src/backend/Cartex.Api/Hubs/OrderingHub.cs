using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

/// Navbat signali filialga cheklangan: `Clients.All` bilan bitta do'kondagi savat
/// o'zgarishi boshqa biznesdagi kassalarni ham qayta so'rovga majburlardi.
[Authorize]
public sealed class OrderingHub(ICurrentUser currentUser, HubPresence presence) : PresenceHub(presence)
{
    private const string BranchKey = "ordering-branch";

    public async Task Subscribe(long branchId)
    {
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new HubException("Branch access denied.");
        if (Context.Items.TryGetValue(BranchKey, out var previous) && previous is long previousBranchId)
        {
            if (previousBranchId == branchId) return;
            await LeaveAsync(HubChannels.CartFeed(previousBranchId));
        }
        await JoinAsync(HubChannels.CartFeed(branchId));
        Context.Items[BranchKey] = branchId;
    }
}

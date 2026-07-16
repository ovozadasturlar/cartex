using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

[Authorize]
public sealed class OrderingHub : Hub;

using Cartex.Application.Shifts.Commands;
using Cartex.Application.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Application.Tests.Common;

public static class TestShift
{
    public static async Task<long> OpenAsync(DatabaseFixture fixture, decimal openingFloat = 0)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new OpenShiftCommand(openingFloat));
    }
}

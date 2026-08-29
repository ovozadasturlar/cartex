using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Settings.Commands;

public record StartStorageMigrationCommand(StorageMigrationDirection Direction) : ICommand<Unit>;

public sealed class StartStorageMigrationCommandHandler(ISettingsService settings, IStorageMigrator migrator, IAuditService audit)
    : IRequestHandler<StartStorageMigrationCommand, Unit>
{
    public async Task<Unit> Handle(StartStorageMigrationCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        if (cfg is not { Enabled: true } || string.IsNullOrWhiteSpace(cfg.Endpoint) || string.IsNullOrWhiteSpace(cfg.Bucket))
            throw new BusinessRuleException("Avval MinIO sozlamalarini kiritib saqlang.");

        migrator.Start(request.Direction);
        audit.Add("settings", "settings", null, new { section = "storageMigrate", direction = request.Direction.ToString() });
        return Unit.Value;
    }
}

public record GetStorageMigrationStatusQuery : IRequest<StorageMigrationStatus>;

public sealed class GetStorageMigrationStatusQueryHandler(IStorageMigrator migrator)
    : IRequestHandler<GetStorageMigrationStatusQuery, StorageMigrationStatus>
{
    public Task<StorageMigrationStatus> Handle(GetStorageMigrationStatusQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(migrator.Status);
}

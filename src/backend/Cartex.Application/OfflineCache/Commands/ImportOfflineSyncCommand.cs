using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using FluentValidation;

namespace Cartex.Application.OfflineCache.Commands;

public sealed record ImportOfflineSyncCommand(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    IReadOnlyList<OfflineSyncEventRequest> Events,
    bool SkipRejected,
    IReadOnlyList<Guid>? SkipEventIds = null) : ICommand<OfflineSyncBatchResult>;

public sealed class ImportOfflineSyncCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISender sender)
    : IRequestHandler<ImportOfflineSyncCommand, OfflineSyncBatchResult>
{
    public async Task<OfflineSyncBatchResult> Handle(ImportOfflineSyncCommand request, CancellationToken cancellationToken)
    {
        // OFF-41: import lease egaligini token bilan isbotlaydi, qurilma mosligi shart emas;
        // chaqiruvchidan lease boshqaruvi ruxsati talab qilinadi.
        if (!currentUser.HasPermission(AppPermissions.Devices.Revoke))
            throw new ForbiddenException("Oflayn navbatni import qilishga ruxsat yo'q.");
        var lease = await OfflineLeaseSecurity.RequireForImportAsync(db, currentUser,
            request.LeaseId, request.Epoch, request.LeaseToken, false, cancellationToken);
        var lastAccepted = lease.LastAcceptedSequence;
        var results = new List<OfflineSyncEventResult>(request.Events.Count);

        for (var index = 0; index < request.Events.Count; index++)
        {
            var row = request.Events[index];
            OfflineSyncEventResult outcome;
            // OFF-44: tanlovdan chiqarilgan amal izsiz o'chirilmaydi — Skipped bo'lib qayd etiladi.
            if (request.SkipEventIds?.Contains(row.EventId) == true)
            {
                try
                {
                    outcome = await sender.Send(new SkipOfflineSyncEventCommand(
                        request.LeaseId, request.Epoch, request.LeaseToken, row,
                        "import selection", ForImport: true), cancellationToken);
                }
                catch (DomainException ex) when (ex.Code == "offline_event_already_applied")
                {
                    outcome = new OfflineSyncEventResult(row.EventId, row.Sequence, "AlreadyApplied");
                }
                results.Add(outcome);
                lastAccepted = Math.Max(lastAccepted, outcome.Sequence);
                continue;
            }
            try
            {
                outcome = await sender.Send(new ApplyOfflineSyncEventCommand(
                    request.LeaseId, request.Epoch, request.LeaseToken, row, ForImport: true), cancellationToken);
            }
            catch (Exception ex) when (ex is DomainException or ValidationException)
            {
                var code = (ex as DomainException)?.Code ?? "validation_error";
                if (!request.SkipRejected)
                {
                    results.Add(new OfflineSyncEventResult(row.EventId, row.Sequence, "Rejected",
                        ErrorCode: code, Error: ex.Message));
                    AddDeferred(index + 1);
                    break;
                }
                // OFF-43: auto-skip — rad etilgan amal Skipped deb qayd etiladi va zanjir davom etadi.
                var skipped = await sender.Send(new SkipOfflineSyncEventCommand(
                    request.LeaseId, request.Epoch, request.LeaseToken, row,
                    $"import auto-skip: {code}", ForImport: true), cancellationToken);
                outcome = skipped with { ErrorCode = code, Error = ex.Message };
            }
            results.Add(outcome);
            lastAccepted = Math.Max(lastAccepted, outcome.Sequence);
        }

        return new OfflineSyncBatchResult(request.LeaseId, request.Epoch, lastAccepted,
            DateTime.UtcNow, results);

        void AddDeferred(int start)
        {
            for (var i = start; i < request.Events.Count; i++)
            {
                var deferred = request.Events[i];
                results.Add(new OfflineSyncEventResult(deferred.EventId, deferred.Sequence, "Deferred",
                    ErrorCode: "prior_event_rejected",
                    Error: "Oldingi amal tuzatilmaguncha navbat davom ettirilmaydi."));
            }
        }
    }
}

public sealed class ImportOfflineSyncCommandValidator : AbstractValidator<ImportOfflineSyncCommand>
{
    public ImportOfflineSyncCommandValidator()
    {
        RuleFor(x => x.LeaseId).GreaterThan(0);
        RuleFor(x => x.Epoch).GreaterThan(0);
        RuleFor(x => x.LeaseToken).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Events).NotEmpty().Must(x => x.Count <= 1000)
            .WithMessage("Bir importda ko'pi bilan 1000 ta amal yuklanadi.");
    }
}

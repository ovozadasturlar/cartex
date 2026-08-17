using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainCapability = Cartex.Domain.Enums.PrintCapability;
using DomainEndpointStatus = Cartex.Domain.Enums.PrinterEndpointStatus;
using DomainJobKind = Cartex.Domain.Enums.PrintJobKind;
using DomainJobStatus = Cartex.Domain.Enums.PrintJobStatus;
using DomainNodeStatus = Cartex.Domain.Enums.PrintNodeStatus;
using DomainAttemptStatus = Cartex.Domain.Enums.PrintAttemptStatus;
using DomainStickyMode = Cartex.Domain.Enums.PrintStickyMode;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Printing;

public record RegisterPrintNodeCommand(RegisterPrintNodeRequest Request) : ICommand<RegisterPrintNodeResult>;

public sealed class RegisterPrintNodeCommandValidator : AbstractValidator<RegisterPrintNodeCommand>
{
    public RegisterPrintNodeCommandValidator()
    {
        RuleFor(x => x.Request.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.DeviceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.ClientVersion).MaximumLength(40);
        RuleFor(x => x.Request.HostToken).MaximumLength(128);
        RuleFor(x => x.Request.Endpoints).Must(x => x.Count <= 64);
        RuleForEach(x => x.Request.Endpoints).ChildRules(endpoint =>
        {
            endpoint.RuleFor(x => x.StableKey).NotEmpty().MaximumLength(256);
            endpoint.RuleFor(x => x.SystemName).NotEmpty().MaximumLength(260);
            endpoint.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
            endpoint.RuleFor(x => x.Capabilities).NotEqual(Cartex.Shared.Models.Printing.PrintCapability.None);
            endpoint.RuleFor(x => x.Status).IsInEnum();
            endpoint.RuleFor(x => x.ProfileJson).MaximumLength(16384);
        });
    }
}

/// Authority lives in the trust flag on the device record, not in the secret: the
/// credential is only a cache that proves "same machine as last time". When it is lost
/// (reinstall, new Windows profile) a replacement is issued silently and the device keeps
/// whatever trust the administrator gave it. When the machine identity itself changes but
/// the old credential is still presented, the existing record is renamed instead of a
/// duplicate appearing. Every such transition is written to the audit log.
public sealed class RegisterPrintNodeCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<RegisterPrintNodeCommand, RegisterPrintNodeResult>
{
    private static readonly TimeSpan ActiveHolderWindow = TimeSpan.FromSeconds(90);

    public async Task<RegisterPrintNodeResult> Handle(RegisterPrintNodeCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        PrintingGuard.EnsureDevice(currentUser, request.DeviceId);
        PrintingGuard.EnsureNodeRegistration(request.DeviceId, request.DeviceName, request.Endpoints);
        var now = DateTime.UtcNow;
        var node = await db.PrintNodes.Include(x => x.Endpoints)
            .FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId, cancellationToken);

        if (node is null && !string.IsNullOrWhiteSpace(request.HostToken))
        {
            var suppliedHash = PrintingCredential.Hash(request.HostToken);
            node = await db.PrintNodes.Include(x => x.Endpoints)
                .FirstOrDefaultAsync(x => x.CredentialHash == suppliedHash, cancellationToken);
            if (node is not null)
            {
                var previousDeviceId = node.DeviceId;
                node.DeviceId = request.DeviceId;
                var requesterRows = await db.PrintRequesterDevices
                    .Where(x => x.DeviceId == previousDeviceId || x.DeviceId == request.DeviceId)
                    .ToListAsync(cancellationToken);
                foreach (var old in requesterRows.Where(x => x.DeviceId == previousDeviceId))
                {
                    var replacement = requesterRows.FirstOrDefault(x =>
                        x.BranchId == old.BranchId && x.DeviceId == request.DeviceId);
                    if (replacement is null)
                    {
                        old.DeviceId = request.DeviceId;
                    }
                    else
                    {
                        replacement.IsTrusted |= old.IsTrusted;
                        db.PrintRequesterDevices.Remove(old);
                    }
                }
                audit.Add("print.node_renamed", "print_nodes", node.Id,
                    new { previousDeviceId, request.DeviceId, node.Name });
            }
        }

        string? issuedToken = null;
        if (node is null)
        {
            // A device already trusted as a requester is the same physical machine, so the
            // freshly appearing host side inherits that trust instead of asking again.
            var autoTrust = await db.Branches.Where(x => x.Id == request.BranchId)
                .Select(x => x.AutoTrustPrintDevices).FirstOrDefaultAsync(cancellationToken);
            var trustedRequester = await db.PrintRequesterDevices.AnyAsync(
                x => x.BranchId == request.BranchId && x.DeviceId == request.DeviceId && x.IsTrusted,
                cancellationToken);
            issuedToken = PrintingCredential.Issue();
            node = new PrintNode
            {
                DeviceId = request.DeviceId,
                CredentialHash = PrintingCredential.Hash(issuedToken),
                CredentialIssuedAt = now,
                BranchId = request.BranchId,
                Name = request.DeviceName,
                IsTrusted = autoTrust || trustedRequester
            };
            db.PrintNodes.Add(node);
            audit.Add("print.node_registered", "print_nodes", null,
                new { request.DeviceId, request.DeviceName, node.IsTrusted });
        }
        else if (!PrintingCredential.Matches(node.CredentialHash, request.HostToken))
        {
            // A machine that is alive right now still owns the credential; a second machine
            // claiming the same identity must not be able to steal it from under it.
            if (node.LastSeenAt > now - ActiveHolderWindow)
                throw new BusinessRuleException("Bu qurilma nomi hozir boshqa faol kompyuter tomonidan ishlatilmoqda.");
            issuedToken = PrintingCredential.Issue();
            node.CredentialHash = PrintingCredential.Hash(issuedToken);
            node.CredentialIssuedAt = now;
            audit.Add("print.node_credential_rotated", "print_nodes", node.Id,
                new { request.DeviceId, node.Name, currentUser.IpAddress });
        }

        node.BranchId = request.BranchId;
        node.Name = request.DeviceName.Trim();
        node.ClientVersion = request.ClientVersion;
        node.HostEnabled = request.HostEnabled;
        node.Status = DomainNodeStatus.Online;
        node.LastSeenAt = now;
        node.LastConnectedAt = now;
        node.LastUserId = currentUser.UserId;
        node.LastClient = currentUser.Client;
        node.LastIpAddress = currentUser.IpAddress;

        PrintingNodeUpdater.UpdateEndpoints(node, request.Endpoints, now);
        await db.SaveChangesAsync(cancellationToken);
        return new RegisterPrintNodeResult(PrintingMapper.Node(node), issuedToken);
    }
}

public record HeartbeatPrintNodeCommand(PrintNodeHeartbeatRequest Request) : ICommand<Unit>;

public sealed class HeartbeatPrintNodeCommandValidator : AbstractValidator<HeartbeatPrintNodeCommand>
{
    public HeartbeatPrintNodeCommandValidator()
    {
        RuleFor(x => x.Request.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.HostToken).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.Endpoints).Must(x => x.Count <= 64);
        RuleForEach(x => x.Request.Endpoints).ChildRules(endpoint =>
        {
            endpoint.RuleFor(x => x.StableKey).NotEmpty().MaximumLength(256);
            endpoint.RuleFor(x => x.SystemName).NotEmpty().MaximumLength(260);
            endpoint.RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
            endpoint.RuleFor(x => x.Capabilities).NotEqual(Cartex.Shared.Models.Printing.PrintCapability.None);
            endpoint.RuleFor(x => x.Status).IsInEnum();
            endpoint.RuleFor(x => x.ProfileJson).MaximumLength(16384);
        });
    }
}

public sealed class HeartbeatPrintNodeCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<HeartbeatPrintNodeCommand, Unit>
{
    public async Task<Unit> Handle(HeartbeatPrintNodeCommand command, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureDevice(currentUser, command.Request.DeviceId);
        var node = await db.PrintNodes.Include(x => x.Endpoints)
            .FirstOrDefaultAsync(x => x.DeviceId == command.Request.DeviceId, cancellationToken)
            ?? throw new NotFoundException("Print node not found.");
        PrintingCredential.Ensure(node, command.Request.HostToken);
        PrintingGuard.EnsureBranch(currentUser, node.BranchId);
        var now = DateTime.UtcNow;
        node.Status = DomainNodeStatus.Online;
        node.LastSeenAt = now;
        node.LastUserId = currentUser.UserId;
        node.LastClient = currentUser.Client;
        node.LastIpAddress = currentUser.IpAddress;
        PrintingNodeUpdater.UpdateEndpoints(node, command.Request.Endpoints, now);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record SetPrintNodeStateCommand(long Id, SetPrintNodeStateRequest Request) : ICommand<Unit>;

public sealed class SetPrintNodeStateCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPrintNodeStateCommand, Unit>
{
    public async Task<Unit> Handle(SetPrintNodeStateCommand command, CancellationToken cancellationToken)
    {
        var node = await db.PrintNodes.FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Print node not found.");
        PrintingGuard.EnsureBranch(currentUser, node.BranchId);
        node.IsTrusted = command.Request.IsTrusted;
        audit.Add(node.IsTrusted ? "print.allow" : "print.block", "print_nodes",
            node.Id, new { node.DeviceId, node.Name });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// One switch for one physical device: the requester record and the host node share the
/// device id, so the administrator never has to understand that they are two rows.
public record SetPrintDeviceTrustCommand(SetPrintDeviceTrustRequest Request) : ICommand<Unit>;

public sealed class SetPrintDeviceTrustCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPrintDeviceTrustCommand, Unit>
{
    public async Task<Unit> Handle(SetPrintDeviceTrustCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        if (string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Length > 64)
            throw new BusinessRuleException("Invalid device id.");
        var node = await db.PrintNodes.FirstOrDefaultAsync(
            x => x.BranchId == request.BranchId && x.DeviceId == request.DeviceId, cancellationToken);
        var requester = await db.PrintRequesterDevices.FirstOrDefaultAsync(
            x => x.BranchId == request.BranchId && x.DeviceId == request.DeviceId, cancellationToken);
        if (node is null && requester is null)
            throw new NotFoundException("Print device not found.");
        if (node is not null) node.IsTrusted = request.IsTrusted;
        if (requester is not null) requester.IsTrusted = request.IsTrusted;
        audit.Add(request.IsTrusted ? "print.allow" : "print.block", "print_devices",
            node?.Id ?? requester!.Id, new { request.DeviceId, Name = node?.Name ?? requester!.Name });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// Removing a device only clears the list: printing history keeps its rows and merely
/// loses the link to the deleted device. If the machine ever connects again it simply
/// shows up as a new device.
public record DeletePrintDeviceCommand(long BranchId, string DeviceId) : ICommand<Unit>;

public sealed class DeletePrintDeviceCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<DeletePrintDeviceCommand, Unit>
{
    public async Task<Unit> Handle(DeletePrintDeviceCommand command, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, command.BranchId);
        var node = await db.PrintNodes.Include(x => x.Endpoints).FirstOrDefaultAsync(
            x => x.BranchId == command.BranchId && x.DeviceId == command.DeviceId, cancellationToken);
        var requesters = await db.PrintRequesterDevices
            .Where(x => x.BranchId == command.BranchId && x.DeviceId == command.DeviceId)
            .ToListAsync(cancellationToken);
        if (node is null && requesters.Count == 0)
            throw new NotFoundException("Print device not found.");
        if (node is not null) db.PrintNodes.Remove(node);
        db.PrintRequesterDevices.RemoveRange(requesters);
        audit.Add("print.device_deleted", "print_devices", node?.Id ?? requesters[0].Id,
            new { command.DeviceId, Name = node?.Name ?? requesters[0].Name });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record SetPrintAutoTrustCommand(SetPrintAutoTrustRequest Request) : ICommand<Unit>;

public sealed class SetPrintAutoTrustCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPrintAutoTrustCommand, Unit>
{
    public async Task<Unit> Handle(SetPrintAutoTrustCommand command, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, command.Request.BranchId);
        var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == command.Request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch not found.");
        branch.AutoTrustPrintDevices = command.Request.Enabled;
        audit.Add("print.auto_trust", "branches", branch.Id, new { command.Request.Enabled });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record SetPrintRequesterDeviceTrustCommand(long Id, SetPrintRequesterDeviceTrustRequest Request) : ICommand<Unit>;

public sealed class SetPrintRequesterDeviceTrustCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPrintRequesterDeviceTrustCommand, Unit>
{
    public async Task<Unit> Handle(SetPrintRequesterDeviceTrustCommand command, CancellationToken cancellationToken)
    {
        var device = await db.PrintRequesterDevices.FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Print requester device not found.");
        PrintingGuard.EnsureBranch(currentUser, device.BranchId);
        device.IsTrusted = command.Request.IsTrusted;
        audit.Add(device.IsTrusted ? "print.allow" : "print.block", "print_requester_devices",
            device.Id, new { device.DeviceId, device.Name, device.Client });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record SetPrinterEndpointCommand(long Id, SetPrinterEndpointRequest Request) : ICommand<Unit>;

public sealed class SetPrinterEndpointCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SetPrinterEndpointCommand, Unit>
{
    public async Task<Unit> Handle(SetPrinterEndpointCommand command, CancellationToken cancellationToken)
    {
        var endpoint = await db.PrinterEndpoints.Include(x => x.PrintNode)
            .FirstOrDefaultAsync(x => x.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException("Printer endpoint not found.");
        PrintingGuard.EnsureBranch(currentUser, endpoint.PrintNode.BranchId);
        endpoint.IsEnabled = command.Request.IsEnabled;
        endpoint.Capabilities = (DomainCapability)command.Request.Capabilities;
        endpoint.DisplayName = string.IsNullOrWhiteSpace(command.Request.DisplayName)
            ? endpoint.SystemName
            : command.Request.DisplayName.Trim();
        endpoint.ProfileJson = command.Request.ProfileJson;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record UpdatePrintRoutingPolicyCommand(long BranchId, Cartex.Shared.Models.Printing.PrintJobKind Kind, UpdatePrintRoutingPolicyRequest Request) : ICommand<PrintRoutingPolicyDto>;

public sealed class UpdatePrintRoutingPolicyCommandValidator : AbstractValidator<UpdatePrintRoutingPolicyCommand>
{
    public UpdatePrintRoutingPolicyCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Request.RoutingMode).IsInEnum();
        RuleFor(x => x.Request.StickyMode).IsInEnum();
        RuleFor(x => x.Request.StickyDurationSeconds).InclusiveBetween(0, 2592000);
        RuleFor(x => x.Request.MaxCopies).InclusiveBetween(1, 100);
        RuleFor(x => x.Request.MaxJobsPerMinute).InclusiveBetween(1, 1000);
        RuleFor(x => x.Request.MaxCopiesPerMinute).InclusiveBetween(1, 5000);
        RuleFor(x => x.Request.AssignmentTimeoutSeconds).InclusiveBetween(5, 300);
        RuleForEach(x => x.Request.Targets).ChildRules(target =>
        {
            target.RuleFor(x => x.EndpointId).GreaterThan(0);
            target.RuleFor(x => x.Priority).InclusiveBetween(0, 100000);
        });
    }
}

public sealed class UpdatePrintRoutingPolicyCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing) : IRequestHandler<UpdatePrintRoutingPolicyCommand, PrintRoutingPolicyDto>
{
    public async Task<PrintRoutingPolicyDto> Handle(UpdatePrintRoutingPolicyCommand command, CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, command.BranchId);
        var request = command.Request;
        if (request.MaxCopies is < 1 or > 100 || request.MaxJobsPerMinute is < 1 or > 1000
            || request.MaxCopiesPerMinute is < 1 or > 5000 || request.AssignmentTimeoutSeconds is < 5 or > 300
            || request.StickyDurationSeconds is < 0 or > 2592000)
            throw new BusinessRuleException("Invalid printing policy limits.");

        var kind = (DomainJobKind)command.Kind;
        var policy = await routing.GetOrCreatePolicyAsync(command.BranchId, kind, cancellationToken);
        var endpointIds = request.Targets.Select(x => x.EndpointId).Distinct().ToList();
        var endpoints = await db.PrinterEndpoints.Include(x => x.PrintNode)
            .Where(x => endpointIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (endpoints.Count != endpointIds.Count || endpoints.Any(x => x.PrintNode.BranchId != command.BranchId))
            throw new BusinessRuleException("A route can only contain printers from the selected branch.");
        var capability = PrintRoutingService.CapabilityFor(kind);
        if (endpoints.Any(x => (x.Capabilities & capability) != capability))
            throw new BusinessRuleException("A selected printer does not support this print type.");

        policy.IsEnabled = request.IsEnabled;
        policy.RoutingMode = (Cartex.Domain.Enums.PrintRoutingMode)request.RoutingMode;
        policy.AllowFallback = request.AllowFallback;
        policy.StickyMode = (DomainStickyMode)request.StickyMode;
        policy.StickyDurationSeconds = request.StickyDurationSeconds;
        policy.MaxCopies = request.MaxCopies;
        policy.DefaultCopies = Math.Min(policy.DefaultCopies, policy.MaxCopies);
        policy.MaxJobsPerMinute = request.MaxJobsPerMinute;
        policy.MaxCopiesPerMinute = request.MaxCopiesPerMinute;
        policy.AssignmentTimeoutSeconds = request.AssignmentTimeoutSeconds;
        policy.Revision++;
        if (policy.StickyMode == DomainStickyMode.Disabled)
        {
            policy.StickyEndpointId = null;
            policy.StickyUntil = null;
        }

        db.PrintRouteTargets.RemoveRange(policy.Targets);
        policy.Targets = request.Targets.Select(x => new PrintRouteTarget
        {
            PrinterEndpointId = x.EndpointId,
            Priority = x.Priority,
            IsEnabled = x.IsEnabled
        }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return await PrintingMapper.PolicyAsync(db, policy.Id, cancellationToken);
    }
}

public record UpdateReceiptPrintPolicyCommand(
    long BranchId,
    UpdateReceiptPrintPolicyRequest Request) : ICommand<PrintRoutingPolicyDto>;

public sealed class UpdateReceiptPrintPolicyCommandValidator : AbstractValidator<UpdateReceiptPrintPolicyCommand>
{
    public UpdateReceiptPrintPolicyCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.Request.DefaultCopies).InclusiveBetween(1, 100);
        RuleFor(x => x.Request.BranchOverride).NotNull().When(x => x.Request.UseBranchOverride);
        When(x => x.Request.BranchOverride is not null, () =>
        {
            RuleFor(x => x.Request.BranchOverride!.PaperWidth).Must(x => x is 32 or 42 or 48);
            RuleFor(x => x.Request.BranchOverride!.PaperFormat).Must(x => x is "Thermal" or "A5" or "A4");
            RuleFor(x => x.Request.BranchOverride!.HeaderText).MaximumLength(200);
            RuleFor(x => x.Request.BranchOverride!.FooterText).MaximumLength(200);
        });
    }
}

public sealed class UpdateReceiptPrintPolicyCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing,
    IAuditService audit) : IRequestHandler<UpdateReceiptPrintPolicyCommand, PrintRoutingPolicyDto>
{
    public async Task<PrintRoutingPolicyDto> Handle(
        UpdateReceiptPrintPolicyCommand command,
        CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureBranch(currentUser, command.BranchId);
        var policy = await routing.GetOrCreatePolicyAsync(command.BranchId, DomainJobKind.Receipt, cancellationToken);
        if (command.Request.ExpectedRevision is { } expected && expected != policy.Revision)
            throw new ConflictException("Chop etish sozlamasi boshqa qurilmada o'zgartirilgan. Yangilab qayta urinib ko'ring.");

        policy.AutoPrintOnSale = command.Request.AutoPrintOnSale;
        policy.DefaultCopies = Math.Clamp(command.Request.DefaultCopies, 1, policy.MaxCopies);
        policy.ReceiptSettingsOverrideJson = command.Request.UseBranchOverride
            ? ReceiptPrintPolicyService.SerializeOverride(command.Request.BranchOverride!)
            : null;
        policy.Revision++;
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("printing.receipt_policy_updated", "print_routing_policies", policy.Id, new
        {
            policy.BranchId,
            policy.AutoPrintOnSale,
            policy.DefaultCopies,
            UseBranchOverride = policy.ReceiptSettingsOverrideJson is not null,
            policy.Revision
        }, "Chek chop etish siyosati yangilandi", policy.BranchId);
        return await PrintingMapper.PolicyAsync(db, policy.Id, cancellationToken);
    }
}

public record CreatePrintJobCommand(CreatePrintJobRequest Request) : ICommand<PrintJobDto>;

public sealed class CreatePrintJobCommandValidator : AbstractValidator<CreatePrintJobCommand>
{
    public CreatePrintJobCommandValidator()
    {
        RuleFor(x => x.Request.BranchId).GreaterThan(0);
        RuleFor(x => x.Request.Kind).IsInEnum();
        RuleFor(x => x.Request.SourceType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Request.SourceId).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Request.Copies).InclusiveBetween(1, 100);
        RuleFor(x => x.Request.Reason).MaximumLength(500);
        RuleFor(x => x.Request.IdempotencyKey).MaximumLength(128);
        RuleFor(x => x.Request.DeviceId).MaximumLength(64);
        RuleFor(x => x.Request.DeviceName).MaximumLength(200);
        RuleFor(x => x.Request.Reason).NotEmpty().When(x => x.Request.IsReprint);
    }
}

public sealed class CreatePrintJobCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    PrintRoutingService routing,
    IPrintJobNotifier notifier,
    IAuditService audit) : IRequestHandler<CreatePrintJobCommand, PrintJobDto>
{
    public async Task<PrintJobDto> Handle(CreatePrintJobCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        PrintingGuard.EnsureBranch(currentUser, request.BranchId);
        PrintingGuard.EnsurePrintPermission(currentUser, request.Kind, request.IsReprint);
        if (!currentUser.HasPermission(AppPermissions.Printing.RemoteUse))
            throw new ForbiddenException("Remote printing permission is required.");

        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceId))
            throw new BusinessRuleException("Print source is required.");

        // SOZ-10: a shop that does not want money slips must be refused here, not merely have
        // the button hidden — the client is not where a policy is enforced.
        if (request.SourceType is "customer_payment" or "customer_refund"
            && !(await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                 ?? new SalesPolicySettings()).PrintMoneyDocuments)
            throw new BusinessRuleException("To'lov hujjatini chop etish o'chirilgan.", "money_document_print_disabled");

        // SOZ-14: proforma do'kon ishida bor-yo'qligi — eganing qarori, shuning uchun
        // yo'naltirish sozlamasi emas, savdo siyosati to'sadi.
        if ((DomainJobKind)request.Kind == DomainJobKind.CartProforma
            && !(await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                 ?? new SalesPolicySettings()).PrintCartProforma)
            throw new BusinessRuleException("Oldindan chop etish o'chirilgan.", "proforma_print_disabled");
        if (request.IsReprint && string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("A reprint reason is required.");
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
            PrintingGuard.EnsureDevice(currentUser, request.DeviceId);

        var kind = (DomainJobKind)request.Kind;
        var policy = await routing.GetOrCreatePolicyAsync(request.BranchId, kind, cancellationToken);
        if (!policy.IsEnabled) throw new BusinessRuleException("Printing is disabled for this print type.");
        if (request.Copies < 1 || request.Copies > policy.MaxCopies)
            throw new BusinessRuleException($"Copy count must be between 1 and {policy.MaxCopies}.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await db.PrintJobs.FirstOrDefaultAsync(x => x.BranchId == request.BranchId
                && x.Kind == kind && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null) return PrintingMapper.Job(existing);
        }

        var since = DateTime.UtcNow.AddMinutes(-1);
        var recent = await db.PrintJobs.Where(x => x.BranchId == request.BranchId
                && x.RequestedByUserId == currentUser.UserId && x.CreatedAt >= since)
            .GroupBy(_ => 1).Select(x => new { Jobs = x.Count(), Copies = x.Sum(j => j.Copies) })
            .FirstOrDefaultAsync(cancellationToken);
        if ((recent?.Jobs ?? 0) >= policy.MaxJobsPerMinute
            || (recent?.Copies ?? 0) + request.Copies > policy.MaxCopiesPerMinute)
            throw new BusinessRuleException("Printing rate limit exceeded.");

        var now = DateTime.UtcNow;
        var deviceId = (currentUser.DeviceId ?? request.DeviceId)?.Trim();
        PrintRequesterDevice? requesterDevice = null;
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            requesterDevice = await db.PrintRequesterDevices.FirstOrDefaultAsync(
                x => x.BranchId == request.BranchId && x.DeviceId == deviceId, cancellationToken);
            if (requesterDevice is null)
            {
                // One physical machine, one trust decision: a device whose host node is
                // already trusted must not be refused when its first request arrives.
                requesterDevice = new PrintRequesterDevice
                {
                    BranchId = request.BranchId,
                    DeviceId = deviceId,
                    FirstSeenAt = now,
                    IsTrusted = await db.Branches.Where(x => x.Id == request.BranchId)
                            .Select(x => x.AutoTrustPrintDevices).FirstOrDefaultAsync(cancellationToken)
                        || await db.PrintNodes.AnyAsync(
                            x => x.BranchId == request.BranchId && x.DeviceId == deviceId && x.IsTrusted,
                            cancellationToken)
                };
                db.PrintRequesterDevices.Add(requesterDevice);
            }
            var deviceName = currentUser.DeviceName ?? request.DeviceName;
            requesterDevice.Name = string.IsNullOrWhiteSpace(deviceName) ? deviceId : deviceName.Trim();
            requesterDevice.Client = currentUser.Client;
            requesterDevice.LastSeenAt = now;
            requesterDevice.LastUserId = currentUser.UserId;
            requesterDevice.LastIpAddress = currentUser.IpAddress;
        }

        var trustedRequester = requesterDevice?.IsTrusted == true;
        var originNodeId = string.IsNullOrWhiteSpace(deviceId)
            ? null
            : await db.PrintNodes.Where(x => x.DeviceId == deviceId && x.BranchId == request.BranchId)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        var job = new PrintJob
        {
            BranchId = request.BranchId,
            Kind = kind,
            SourceType = request.SourceType.Trim(),
            SourceId = request.SourceId.Trim(),
            PayloadJson = "{}",
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            Copies = request.Copies,
            IsReprint = request.IsReprint,
            Reason = request.Reason?.Trim(),
            RequestedByUserId = currentUser.UserId ?? throw new ForbiddenException("Not authenticated."),
            RequestedDeviceId = deviceId,
            RequestedDeviceName = currentUser.DeviceName ?? request.DeviceName,
            RequestedClient = currentUser.Client,
            RequestedIpAddress = currentUser.IpAddress,
            RequestedUserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            OriginNodeId = originNodeId
        };
        if (!trustedRequester)
        {
            job.Status = DomainJobStatus.Rejected;
            job.ErrorCode = "REQUEST_DEVICE_NOT_APPROVED";
            job.ErrorMessage = "This device is not approved to send print jobs.";
            db.PrintJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);
            audit.SetOutcome("print.rejected", "print_jobs", job.Id, new
            {
                job.Kind,
                job.SourceType,
                job.SourceId,
                job.Copies,
                job.IsReprint,
                job.Reason,
                job.ErrorCode
            }, "Chop etish topshirig'i rad etildi", job.BranchId);
            return PrintingMapper.Job(job);
        }

        job.PayloadJson = await PrintingPayloadValidator.ValidateAsync(
            db, settings, request.BranchId, kind, request.SourceType, request.SourceId, request.Payload,
            cancellationToken);
        // A job printed on the requesting machine while the server was unreachable arrives
        // later purely as history: it is stored completed and never routed again.
        if (request.CompletedLocally)
        {
            job.Status = DomainJobStatus.Completed;
            job.CompletedAt = now;
            job.AssignedNodeId = originNodeId;
            db.PrintJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);
            audit.SetOutcome("print.local_completed", "print_jobs", job.Id, new
            {
                job.Kind,
                job.SourceType,
                job.SourceId,
                job.Copies,
                job.RequestedDeviceId
            }, "Chop etish serversiz lokal bajarilgan", job.BranchId);
            return PrintingMapper.Job(job);
        }

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome(job.IsReprint ? "print.reprint_requested" : "print.requested", "print_jobs", job.Id, new
        {
            job.Kind,
            job.SourceType,
            job.SourceId,
            job.Copies,
            job.IsReprint,
            job.Reason,
            job.RequestedDeviceId
        }, job.IsReprint ? "Qayta chop etish topshirig'i yaratildi" : "Chop etish topshirig'i yaratildi", job.BranchId);
        if (await routing.AssignAsync(job, cancellationToken) && job.AssignedNodeId is not null)
        {
            var targetDeviceId = await db.PrintNodes.Where(x => x.Id == job.AssignedNodeId)
                .Select(x => x.DeviceId).FirstAsync(cancellationToken);
            await notifier.NotifyJobAvailableAsync(targetDeviceId, job.Id, cancellationToken);
        }
        return PrintingMapper.Job(job);
    }
}

internal static class PrintingPayloadValidator
{
    public static async Task<string> ValidateAsync(
        IApplicationDbContext db,
        ISettingsService settings,
        long branchId,
        DomainJobKind kind,
        string sourceType,
        string sourceId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.GetRawText().Length > 32768) throw new BusinessRuleException("Print payload is too large.");
        return kind switch
        {
            DomainJobKind.Receipt => await ReceiptAsync(db, settings, branchId, sourceType, sourceId, payload, cancellationToken),
            DomainJobKind.BarcodeLabel => await BarcodeAsync(db, settings, payload, cancellationToken),
            DomainJobKind.ZReport => await ZReportAsync(db, branchId, sourceId, payload, cancellationToken),
            DomainJobKind.Document => await DocumentAsync(db, branchId, payload, cancellationToken),
            DomainJobKind.CartProforma => await CartProformaAsync(db, settings, branchId, sourceId, payload, cancellationToken),
            _ => throw new BusinessRuleException("Unsupported print payload.")
        };
    }

    private static async Task<string> ReceiptAsync(
        IApplicationDbContext db,
        ISettingsService settings,
        long branchId,
        string sourceType,
        string sourceId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        // A return receipt is printed on the same paper as a sale receipt, so it shares this
        // kind — but it must be validated against the return document, not against Sales.
        if (sourceType == "customer_return")
        {
            var returnId = Number(payload, "returnId") ?? (long.TryParse(sourceId, out var id) ? id : 0);
            if (returnId <= 0 || !await db.CustomerReturnDocuments
                    .AnyAsync(x => x.Id == returnId && x.BranchId == branchId, cancellationToken))
                throw new NotFoundException("Return document not found.");
            return ReceiptPrintPolicyService.SerializeReturnPayload(
                returnId, await ReceiptConfigAsync(db, settings, branchId, cancellationToken));
        }

        var token = Text(payload, "receiptToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            var saleId = Number(payload, "saleId") ?? (long.TryParse(sourceId, out var value) ? value : 0);
            token = await db.Sales.Where(x => x.BranchId == branchId && x.Id == saleId)
                .Select(x => x.ReceiptToken).FirstOrDefaultAsync(cancellationToken);
        }
        else if (!await db.Sales.AnyAsync(x => x.BranchId == branchId && x.ReceiptToken == token, cancellationToken))
        {
            token = null;
        }
        if (string.IsNullOrWhiteSpace(token)) throw new NotFoundException("Receipt not found.");
        return ReceiptPrintPolicyService.SerializeReceiptPayload(
            token, await ReceiptConfigAsync(db, settings, branchId, cancellationToken));
    }

    private static async Task<ReceiptSettings> ReceiptConfigAsync(
        IApplicationDbContext db,
        ISettingsService settings,
        long branchId,
        CancellationToken cancellationToken)
    {
        var configured = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new();
        var policy = await db.PrintRoutingPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Kind == DomainJobKind.Receipt,
                cancellationToken);
        if (ReceiptPrintPolicyService.DeserializeOverride(policy?.ReceiptSettingsOverrideJson) is { } branchOverride)
            configured = ReceiptPrintPolicyService.FromDto(branchOverride);
        var notification = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        configured.PublicReceiptBaseUrl = notification?.PublicBaseUrl;
        return configured;
    }

    private static async Task<string> BarcodeAsync(IApplicationDbContext db, ISettingsService settings, JsonElement payload, CancellationToken cancellationToken)
    {
        var code = Required(payload, "code", 128);
        var name = Required(payload, "name", 300);
        if (!await db.Barcodes.AnyAsync(x => x.Code == code, cancellationToken))
            throw new NotFoundException("Barcode not found.");
        var priceText = Text(payload, "priceText");
        var sku = Text(payload, "sku");
        var configured = await settings.GetAsync<BarcodeLabelSettings>(SettingKeys.BarcodeLabel, cancellationToken) ?? new();
        var requestedWithPrice = Boolean(payload, "withPrice");
        var withPrice = configured.AllowPriceOverride && requestedWithPrice is not null
            ? requestedWithPrice.Value
            : configured.DefaultWithPrice;
        var showSku = Boolean(payload, "showSku") ?? configured.ShowSku;
        if (priceText?.Length > 80 || sku?.Length > 80) throw new BusinessRuleException("Invalid barcode label payload.");
        return JsonSerializer.Serialize(new
        {
            code,
            name,
            priceText,
            sku,
            withPrice,
            showSku,
            nameLines = configured.NameLines,
            currencyDisplay = configured.CurrencyDisplay,
            currencyCase = configured.CurrencyCase,
            priceCurrencyMode = configured.PriceCurrencyMode
        });
    }

    private static async Task<string> ZReportAsync(IApplicationDbContext db, long branchId, string sourceId, JsonElement payload, CancellationToken cancellationToken)
    {
        var shiftId = Number(payload, "shiftId") ?? (long.TryParse(sourceId, out var value) ? value : 0);
        if (shiftId <= 0 || !await db.Shifts.AnyAsync(x => x.Id == shiftId && x.BranchId == branchId, cancellationToken))
            throw new NotFoundException("Shift not found.");
        return JsonSerializer.Serialize(new { shiftId });
    }

    private static async Task<string> DocumentAsync(
        IApplicationDbContext db,
        long branchId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var token = Required(payload, "receiptToken", 128);
        if (!await db.Sales.AnyAsync(x => x.BranchId == branchId && x.ReceiptToken == token, cancellationToken))
            throw new NotFoundException("Document source not found.");
        return JsonSerializer.Serialize(new { receiptToken = token });
    }

    /// A cart proforma is printed before the sale exists, so it references the saved cart
    /// rather than carrying its contents. The template travels in the payload so every host
    /// prints it the same way regardless of its local settings.
    private static async Task<string> CartProformaAsync(
        IApplicationDbContext db,
        ISettingsService settings,
        long branchId,
        string sourceId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var code = Text(payload, "cartCode")?.Trim() ?? sourceId;
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64
            || !await db.Carts.AnyAsync(x => x.AggregateCode == code && x.BranchId == branchId, cancellationToken))
            throw new NotFoundException("Cart not found.");
        var configured = await settings.GetAsync<ProformaSettings>(SettingKeys.Proforma, cancellationToken) ?? new();
        return JsonSerializer.Serialize(new
        {
            cartCode = code,
            proformaSettings = new
            {
                headerText = configured.HeaderText,
                footerText = configured.FooterText,
                paperWidth = configured.PaperWidth,
                paperFormat = configured.PaperFormat,
                showBusinessName = configured.ShowBusinessName,
                showAddress = configured.ShowAddress,
                showPhone = configured.ShowPhone,
                showSeller = configured.ShowSeller,
                showCustomer = configured.ShowCustomer,
                showNote = configured.ShowNote,
                showCartCode = configured.ShowCartCode
            }
        });
    }

    private static string Required(JsonElement payload, string name, int maxLength)
    {
        var value = Text(payload, name)?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new BusinessRuleException($"Invalid {name}.");
        return value;
    }

    private static string? Text(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;

    private static bool? Boolean(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}

public record AcceptPrintJobCommand(long JobId, PrintJobLeaseRequest Request) : ICommand<Unit>;
public record SubmitPrintJobCommand(long JobId, PrintJobSubmittedRequest Request) : ICommand<Unit>;
public record CompletePrintJobCommand(long JobId, PrintJobLeaseRequest Request) : ICommand<Unit>;
public record FailPrintJobCommand(long JobId, PrintJobFailedRequest Request) : ICommand<Unit>;

public sealed class FailPrintJobCommandValidator : AbstractValidator<FailPrintJobCommand>
{
    public FailPrintJobCommandValidator()
    {
        RuleFor(x => x.JobId).GreaterThan(0);
        RuleFor(x => x.Request.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.LeaseToken).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Request.HostToken).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.ErrorCode).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Request.ErrorMessage).NotEmpty().MaximumLength(1000);
    }
}

public sealed class AcceptPrintJobCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<AcceptPrintJobCommand, Unit>
{
    public async Task<Unit> Handle(AcceptPrintJobCommand command, CancellationToken cancellationToken)
    {
        var (job, attempt, endpoint) = await PrintingLease.LoadAsync(db, currentUser, command.JobId, command.Request.DeviceId, command.Request.LeaseToken, command.Request.HostToken, cancellationToken);
        if (job.Status is DomainJobStatus.Accepted or DomainJobStatus.SpoolSubmitted)
            return Unit.Value;
        if (job.Status != DomainJobStatus.Assigned)
            throw new BusinessRuleException("Print job is not assignable.");
        job.Status = DomainJobStatus.Accepted;
        job.AcceptedAt = DateTime.UtcNow;
        job.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5);
        attempt.Status = DomainAttemptStatus.Accepted;
        attempt.AcceptedAt = job.AcceptedAt;
        endpoint.Status = DomainEndpointStatus.Busy;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SubmitPrintJobCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<SubmitPrintJobCommand, Unit>
{
    public async Task<Unit> Handle(SubmitPrintJobCommand command, CancellationToken cancellationToken)
    {
        var (job, attempt, _) = await PrintingLease.LoadAsync(db, currentUser, command.JobId, command.Request.DeviceId, command.Request.LeaseToken, command.Request.HostToken, cancellationToken);
        if (job.Status is not (DomainJobStatus.Accepted or DomainJobStatus.SpoolSubmitted))
            throw new BusinessRuleException("Print job was not accepted.");
        job.Status = DomainJobStatus.SpoolSubmitted;
        job.SubmittedAt ??= DateTime.UtcNow;
        attempt.Status = DomainAttemptStatus.SpoolSubmitted;
        attempt.SubmittedAt ??= job.SubmittedAt;
        attempt.SpoolJobId = command.Request.SpoolJobId;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class CompletePrintJobCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPrintJobNotifier notifier,
    ILogger<CompletePrintJobCommandHandler> logger)
    : IRequestHandler<CompletePrintJobCommand, Unit>
{
    public async Task<Unit> Handle(CompletePrintJobCommand command, CancellationToken cancellationToken)
    {
        var (job, attempt, endpoint) = await PrintingLease.LoadAsync(db, currentUser, command.JobId, command.Request.DeviceId, command.Request.LeaseToken, command.Request.HostToken, cancellationToken);
        if (job.Status is not (DomainJobStatus.Accepted or DomainJobStatus.SpoolSubmitted or DomainJobStatus.Completed))
            throw new BusinessRuleException("Print job cannot be completed.");
        var now = DateTime.UtcNow;
        job.Status = DomainJobStatus.Completed;
        job.CompletedAt ??= now;
        attempt.Status = DomainAttemptStatus.Completed;
        attempt.CompletedAt ??= now;
        endpoint.Status = DomainEndpointStatus.Ready;
        endpoint.LastSuccessAt = now;
        endpoint.ConsecutiveFailures = 0;
        var policy = await db.PrintRoutingPolicies.FirstOrDefaultAsync(x => x.BranchId == job.BranchId && x.Kind == job.Kind, cancellationToken);
        if (policy is not null && policy.StickyMode != DomainStickyMode.Disabled)
        {
            policy.StickyEndpointId = endpoint.Id;
            policy.StickyUntil = policy.StickyMode == DomainStickyMode.Duration ? now.AddSeconds(policy.StickyDurationSeconds) : null;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(job.RequestedDeviceId))
        {
            try
            {
                await notifier.NotifyJobStatusChangedAsync(job.RequestedDeviceId, new PrintJobStatusUpdate(
                    job.Id,
                    (Cartex.Shared.Models.Printing.PrintJobKind)job.Kind,
                    Cartex.Shared.Models.Printing.PrintJobStatus.Completed,
                    endpoint.DisplayName,
                    null), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not notify device {DeviceId} that print job {JobId} completed", job.RequestedDeviceId, job.Id);
            }
        }
        return Unit.Value;
    }
}

public sealed class FailPrintJobCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    PrintRoutingService routing,
    IPrintJobNotifier notifier,
    ILogger<FailPrintJobCommandHandler> logger) : IRequestHandler<FailPrintJobCommand, Unit>
{
    public async Task<Unit> Handle(FailPrintJobCommand command, CancellationToken cancellationToken)
    {
        var (job, attempt, endpoint) = await PrintingLease.LoadAsync(db, currentUser, command.JobId, command.Request.DeviceId, command.Request.LeaseToken, command.Request.HostToken, cancellationToken);
        var now = DateTime.UtcNow;
        endpoint.Status = DomainEndpointStatus.Error;
        endpoint.LastFailureAt = now;
        endpoint.ConsecutiveFailures++;
        attempt.ErrorCode = command.Request.ErrorCode;
        attempt.ErrorMessage = command.Request.ErrorMessage;
        job.ErrorCode = command.Request.ErrorCode;
        job.ErrorMessage = command.Request.ErrorMessage;

        if (command.Request.WasSubmitted || job.Status == DomainJobStatus.SpoolSubmitted)
        {
            attempt.Status = DomainAttemptStatus.UnknownAfterSubmit;
            job.Status = DomainJobStatus.ManualReview;
            await db.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(job.RequestedDeviceId))
            {
                try
                {
                    await notifier.NotifyJobStatusChangedAsync(job.RequestedDeviceId, new PrintJobStatusUpdate(
                        job.Id,
                        (Cartex.Shared.Models.Printing.PrintJobKind)job.Kind,
                        Cartex.Shared.Models.Printing.PrintJobStatus.ManualReview,
                        endpoint.DisplayName,
                        job.ErrorMessage), cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Could not notify device {DeviceId} that print job {JobId} requires manual review", job.RequestedDeviceId, job.Id);
                }
            }
            return Unit.Value;
        }

        attempt.Status = DomainAttemptStatus.FailedBeforeSubmit;
        attempt.CompletedAt = now;
        job.Status = DomainJobStatus.Pending;
        job.AssignedNodeId = null;
        job.AssignedEndpointId = null;
        job.LeaseToken = null;
        job.LeaseExpiresAt = null;
        var policy = await db.PrintRoutingPolicies.FirstOrDefaultAsync(x => x.BranchId == job.BranchId && x.Kind == job.Kind, cancellationToken);
        if (policy?.StickyMode == DomainStickyMode.UntilFailure && policy.StickyEndpointId == endpoint.Id)
            policy.StickyEndpointId = null;
        await db.SaveChangesAsync(cancellationToken);
        if (await routing.AssignAsync(job, cancellationToken) && job.AssignedNodeId is not null)
        {
            var deviceId = await db.PrintNodes.Where(x => x.Id == job.AssignedNodeId).Select(x => x.DeviceId).FirstAsync(cancellationToken);
            await notifier.NotifyJobAvailableAsync(deviceId, job.Id, cancellationToken);
        }
        return Unit.Value;
    }
}

internal static class PrintingGuard
{
    public static void EnsureBranch(ICurrentUser currentUser, long branchId)
    {
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Branch access denied.");
    }

    public static void EnsureDevice(ICurrentUser currentUser, string deviceId)
    {
        if (!string.IsNullOrWhiteSpace(currentUser.DeviceId)
            && !string.Equals(currentUser.DeviceId, deviceId, StringComparison.Ordinal))
            throw new ForbiddenException("Device identity mismatch.");
    }

    public static void EnsurePrintPermission(ICurrentUser currentUser, Cartex.Shared.Models.Printing.PrintJobKind kind, bool isReprint)
    {
        var permission = kind switch
        {
            Cartex.Shared.Models.Printing.PrintJobKind.Receipt when isReprint => AppPermissions.Printing.ReceiptReprint,
            Cartex.Shared.Models.Printing.PrintJobKind.Receipt => AppPermissions.Printing.ReceiptPrint,
            Cartex.Shared.Models.Printing.PrintJobKind.BarcodeLabel => AppPermissions.Printing.BarcodePrint,
            Cartex.Shared.Models.Printing.PrintJobKind.ZReport => AppPermissions.Printing.ZReportPrint,
            Cartex.Shared.Models.Printing.PrintJobKind.Document => AppPermissions.Printing.DocumentPrint,
            Cartex.Shared.Models.Printing.PrintJobKind.CartProforma => AppPermissions.Printing.DocumentPrint,
            _ => throw new BusinessRuleException("Unsupported print type.")
        };
        if (!currentUser.HasPermission(permission)) throw new ForbiddenException("Print permission denied.");
    }

    public static void EnsureNodeRegistration(
        string deviceId,
        string deviceName,
        IReadOnlyList<PrinterEndpointRegistration> endpoints)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 64
            || string.IsNullOrWhiteSpace(deviceName) || deviceName.Length > 200
            || endpoints.Count > 64)
            throw new BusinessRuleException("Invalid print node registration.");
        if (endpoints.Any(x => string.IsNullOrWhiteSpace(x.StableKey) || x.StableKey.Length > 256
            || string.IsNullOrWhiteSpace(x.SystemName) || x.SystemName.Length > 260
            || string.IsNullOrWhiteSpace(x.DisplayName) || x.DisplayName.Length > 200
            || x.Capabilities == Cartex.Shared.Models.Printing.PrintCapability.None
            || !Enum.IsDefined(x.Status)
            || x.ProfileJson?.Length > 16384))
            throw new BusinessRuleException("Invalid printer endpoint registration.");
    }
}

internal static class PrintingNodeUpdater
{
    public static void UpdateEndpoints(PrintNode node, IReadOnlyList<PrinterEndpointRegistration> registrations, DateTime now)
    {
        var received = registrations.Select(x => x.StableKey).ToHashSet(StringComparer.Ordinal);
        foreach (var endpoint in node.Endpoints.Where(x => !received.Contains(x.StableKey)))
        {
            endpoint.Status = DomainEndpointStatus.Offline;
            endpoint.LastSeenAt = now;
        }
        foreach (var item in registrations)
        {
            var endpoint = node.Endpoints.FirstOrDefault(x => x.StableKey == item.StableKey);
            if (endpoint is null)
            {
                endpoint = new PrinterEndpoint { StableKey = item.StableKey };
                node.Endpoints.Add(endpoint);
            }
            endpoint.SystemName = item.SystemName.Trim();
            endpoint.DisplayName = item.DisplayName.Trim();
            endpoint.Capabilities = (DomainCapability)item.Capabilities;
            endpoint.Status = (DomainEndpointStatus)item.Status;
            endpoint.ProfileJson = item.ProfileJson;
            endpoint.LastSeenAt = now;
        }
    }
}

internal static class PrintingLease
{
    public static async Task<(PrintJob Job, PrintAttempt Attempt, PrinterEndpoint Endpoint)> LoadAsync(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        long jobId,
        string deviceId,
        string leaseToken,
        string hostToken,
        CancellationToken cancellationToken)
    {
        PrintingGuard.EnsureDevice(currentUser, deviceId);
        var job = await db.PrintJobs.Include(x => x.AssignedNode).Include(x => x.AssignedEndpoint)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken)
            ?? throw new NotFoundException("Print job not found.");
        if (job.AssignedNode?.DeviceId != deviceId || job.LeaseToken != leaseToken)
            throw new ForbiddenException("Invalid print job lease.");
        PrintingCredential.Ensure(job.AssignedNode, hostToken);
        PrintingGuard.EnsureBranch(currentUser, job.BranchId);
        var attempt = await db.PrintAttempts.FirstAsync(x => x.PrintJobId == job.Id && x.LeaseToken == leaseToken, cancellationToken);
        return (job, attempt, job.AssignedEndpoint ?? throw new BusinessRuleException("Printer endpoint is missing."));
    }
}

public static class PrintingCredential
{
    public static string Issue() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static void Ensure(PrintNode node, string? token)
    {
        if (!Matches(node.CredentialHash, token))
            throw new ForbiddenException("Invalid print host credential.");
    }

    public static bool Matches(string? expectedHash, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedHash)) return false;
        var supplied = Encoding.UTF8.GetBytes(Hash(token));
        var expected = Encoding.UTF8.GetBytes(expectedHash);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}

internal static class PrintingMapper
{
    public static PrintNodeDto Node(PrintNode node) => new(
        node.Id, node.BranchId, node.DeviceId, node.Name, node.ClientVersion,
        node.IsTrusted, node.HostEnabled, (Cartex.Shared.Models.Printing.PrintNodeStatus)node.Status,
        node.LastSeenAt, node.LastClient, node.Endpoints.OrderBy(x => x.DisplayName).Select(Endpoint).ToList());

    public static PrinterEndpointDto Endpoint(PrinterEndpoint endpoint) => new(
        endpoint.Id, endpoint.PrintNodeId, endpoint.StableKey, endpoint.SystemName, endpoint.DisplayName,
        (Cartex.Shared.Models.Printing.PrintCapability)endpoint.Capabilities,
        (Cartex.Shared.Models.Printing.PrinterEndpointStatus)endpoint.Status, endpoint.IsEnabled,
        endpoint.LastSeenAt, endpoint.LastSuccessAt, endpoint.LastFailureAt, endpoint.ConsecutiveFailures, endpoint.ProfileJson);

    public static PrintJobDto Job(PrintJob job) => new(
        job.Id, job.BranchId, (Cartex.Shared.Models.Printing.PrintJobKind)job.Kind,
        (Cartex.Shared.Models.Printing.PrintJobStatus)job.Status, job.SourceType, job.SourceId,
        job.Copies, job.IsReprint, job.Reason, job.RequestedByUserId, job.RequestedDeviceId,
        job.RequestedDeviceName, job.RequestedClient, job.AssignedNodeId, job.AssignedEndpointId,
        job.AttemptCount, job.CreatedAt, job.CompletedAt, job.ErrorCode, job.ErrorMessage,
        JobSummary(job), job.RequestedByUser?.FullName ?? job.RequestedByUser?.Username,
        job.AssignedNode?.Name, job.AssignedEndpoint?.DisplayName);

    private static string JobSummary(PrintJob job)
    {
        try
        {
            using var document = JsonDocument.Parse(job.PayloadJson);
            var payload = document.RootElement;
            return job.Kind switch
            {
                DomainJobKind.BarcodeLabel => string.Join(" · ", new[]
                    {
                        Text(payload, "name"),
                        Text(payload, "code"),
                        Text(payload, "priceText")
                    }.Where(x => !string.IsNullOrWhiteSpace(x))),
                DomainJobKind.Receipt => $"Receipt #{job.SourceId}",
                DomainJobKind.ZReport => $"Z report #{job.SourceId}",
                DomainJobKind.Document => $"Document #{job.SourceId}",
                DomainJobKind.CartProforma => $"Savat {job.SourceId}",
                _ => job.SourceId
            };
        }
        catch
        {
            return job.SourceId;
        }
    }

    private static string? Text(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static async Task<PrintRoutingPolicyDto> PolicyAsync(IApplicationDbContext db, long policyId, CancellationToken cancellationToken)
    {
        var policy = await db.PrintRoutingPolicies.AsNoTracking().Include(x => x.Targets)
            .ThenInclude(x => x.PrinterEndpoint).ThenInclude(x => x.PrintNode)
            .FirstAsync(x => x.Id == policyId, cancellationToken);
        return new PrintRoutingPolicyDto(policy.Id, policy.BranchId,
            (Cartex.Shared.Models.Printing.PrintJobKind)policy.Kind, policy.IsEnabled,
            (Cartex.Shared.Models.Printing.PrintRoutingMode)policy.RoutingMode, policy.AllowFallback,
            (Cartex.Shared.Models.Printing.PrintStickyMode)policy.StickyMode, policy.StickyDurationSeconds,
            policy.StickyEndpointId, policy.StickyUntil, policy.MaxCopies, policy.MaxJobsPerMinute,
            policy.MaxCopiesPerMinute, policy.AssignmentTimeoutSeconds,
            policy.Targets.OrderBy(x => x.Priority).Select(x => new PrintRouteTargetDto(
                x.PrinterEndpointId, x.PrinterEndpoint.PrintNode.Name, x.PrinterEndpoint.DisplayName,
                (Cartex.Shared.Models.Printing.PrintCapability)x.PrinterEndpoint.Capabilities, x.Priority,
                x.IsEnabled, x.PrinterEndpoint.PrintNode.IsTrusted,
                (Cartex.Shared.Models.Printing.PrinterEndpointStatus)x.PrinterEndpoint.Status)).ToList(),
            policy.AutoPrintOnSale,
            policy.DefaultCopies,
            ReceiptPrintPolicyService.DeserializeOverride(policy.ReceiptSettingsOverrideJson),
            policy.Revision);
    }
}

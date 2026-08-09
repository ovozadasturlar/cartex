using Cartex.Application.Common.Interfaces;
using Cartex.Application.TradeCases.Queries;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.TradeCases.Commands;

public sealed record ExportTradeCaseStatementCommand(
    long TradeCaseId,
    string Format,
    string Mode,
    DateTime? From = null,
    DateTime? To = null) : ICommand<GeneratedDocument>;

public sealed class ExportTradeCaseStatementCommandHandler(
    ICurrentUser currentUser,
    ISender sender,
    ITradeCaseStatementExporter exporter,
    IAuditService audit) : IRequestHandler<ExportTradeCaseStatementCommand, GeneratedDocument>
{
    public async Task<GeneratedDocument> Handle(ExportTradeCaseStatementCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Statements.Export))
            throw new ForbiddenException("Hisob ko'chirmasini eksport qilishga ruxsat yo'q.");
        var statement = await sender.Send(
            new GetTradeCaseStatementQuery(request.TradeCaseId, request.From, request.To), cancellationToken);
        var document = exporter.Export(statement, request.Format, request.Mode);
        audit.SetOutcome("statement.exported", "trade_cases", request.TradeCaseId, new
        {
            request.Format,
            request.Mode,
            request.From,
            request.To,
            document.FileName,
            bytes = document.Content.Length
        }, "Loyiha hisob ko'chirmasi eksport qilindi");
        return document;
    }
}

public sealed class ExportTradeCaseStatementCommandValidator : AbstractValidator<ExportTradeCaseStatementCommand>
{
    public ExportTradeCaseStatementCommandValidator()
    {
        RuleFor(x => x.TradeCaseId).GreaterThan(0);
        RuleFor(x => x.Format).Must(x => x is not null && (x.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                                                           || x.Equals("xlsx", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Format faqat pdf yoki xlsx bo'lishi mumkin.");
        RuleFor(x => x.Mode).Must(x => x is not null && (x.Equals("timeline", StringComparison.OrdinalIgnoreCase)
                                                         || x.Equals("consolidated", StringComparison.OrdinalIgnoreCase)
                                                         || x.Equals("both", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Ko'rinish timeline, consolidated yoki both bo'lishi mumkin.");
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
    }
}

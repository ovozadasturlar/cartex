namespace Cartex.Domain.Enums;

[Flags]
public enum PrintCapability
{
    None = 0,
    Receipt = 1,
    BarcodeLabel = 2,
    ZReport = 4,
    Document = 8
}

public enum PrintJobKind
{
    Receipt,
    BarcodeLabel,
    ZReport,
    Document
}

public enum PrintNodeStatus
{
    Offline,
    Online,
    Degraded
}

public enum PrinterEndpointStatus
{
    Unknown,
    Ready,
    Busy,
    Offline,
    Error
}

public enum PrintRoutingMode
{
    LocalFirst,
    PriorityOnly,
    LocalOnly
}

public enum PrintStickyMode
{
    Disabled,
    Duration,
    UntilFailure,
    Permanent
}

public enum PrintJobStatus
{
    Pending,
    Assigned,
    Accepted,
    SpoolSubmitted,
    Completed,
    Failed,
    ManualReview,
    Cancelled
}

public enum PrintAttemptStatus
{
    Assigned,
    Accepted,
    SpoolSubmitted,
    Completed,
    FailedBeforeSubmit,
    UnknownAfterSubmit
}

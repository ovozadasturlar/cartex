namespace Cartex.Application.Common.Interfaces;

public enum StorageMigrationDirection
{
    LocalToRemote,
    RemoteToLocal
}

public record StorageMigrationStatus(bool Running, string? Direction, int Processed, int Failed, int Total, string? Error, DateTime? FinishedAt);

public interface IStorageMigrator
{
    StorageMigrationStatus Status { get; }
    void Start(StorageMigrationDirection direction);
}

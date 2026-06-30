namespace Cartex.Persistence;

public interface IAuditService
{
    void Add(string action, string table, long? recordId, object? newData = null, object? oldData = null);
}

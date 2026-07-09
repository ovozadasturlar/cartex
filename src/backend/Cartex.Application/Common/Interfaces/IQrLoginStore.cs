namespace Cartex.Application.Common.Interfaces;

public interface IQrLoginStore
{
    string Start(TimeSpan ttl);
    bool Approve(string code, long userId);
    long? TakeApproved(string code);
}

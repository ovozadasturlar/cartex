namespace Cartex.Application.Common.Interfaces;

public interface IQrLoginStore
{
    string Start();
    bool Approve(string code, long userId);
    long? TakeApproved(string code);
}

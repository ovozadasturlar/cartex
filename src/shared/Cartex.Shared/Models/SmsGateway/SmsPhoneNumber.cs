namespace Cartex.Shared.Models.SmsGateway;

public static class SmsPhoneNumber
{
    public static string Normalize(string phone) => new(phone.Where(char.IsDigit).ToArray());
}

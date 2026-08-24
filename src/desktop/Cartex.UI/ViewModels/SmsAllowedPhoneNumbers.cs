using System.Collections.ObjectModel;
using Cartex.Shared.Models.SmsGateway;

namespace Cartex.UI.ViewModels;

public sealed class SmsAllowedPhoneNumbers
{
    public ObservableCollection<string> Items { get; } = [];

    public bool Add(string value)
    {
        var normalized = SmsPhoneNumber.Normalize(value);
        if (normalized.Length < 9 || Items.Contains(normalized))
            return false;
        Items.Add(normalized);
        return true;
    }

    public void Remove(string value) => Items.Remove(value);

    public void Replace(IEnumerable<string> values)
    {
        Items.Clear();
        foreach (var value in values)
            Add(value);
    }
}

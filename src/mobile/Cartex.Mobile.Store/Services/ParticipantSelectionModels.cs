using System.Collections.ObjectModel;
using Cartex.Shared.Models.Partners;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Store.Services;

public sealed class ParticipantRoleSelectionRow(ParticipantRoleDto role) : ObservableObject
{
    public ParticipantRoleDto Role { get; } = role;
    public long Id => Role.Id;
    public string Label => Role.SingularLabel;
    public bool IsRequired => Role.IsRequired;
    public bool CanEqualBuyer => Role.CanEqualBuyer;
    public int MaxCount => Math.Max(1, Role.MaxCount);
    public ObservableCollection<CartParticipantDraft> Selections { get; } = [];
    public bool HasSelections => Selections.Count > 0;
    public bool CanAdd => Selections.Count < MaxCount;
    public string LimitText => MaxCount > 1 ? $"{Selections.Count}/{MaxCount}" : "";

    public void Replace(IEnumerable<CartParticipantDraft> values)
    {
        Selections.Clear();
        foreach (var value in values.Where(x => x.RoleDefinitionId == Id).Take(MaxCount))
            Selections.Add(value);
        Notify();
    }

    public void Add(CartParticipantDraft value)
    {
        if (MaxCount <= 1)
            Selections.Clear();
        if (Selections.All(x => x.PartyId != value.PartyId) && Selections.Count < MaxCount)
            Selections.Add(value);
        Notify();
    }

    public void Remove(long partyId)
    {
        var value = Selections.FirstOrDefault(x => x.PartyId == partyId);
        if (value is not null)
            Selections.Remove(value);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasSelections));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(LimitText));
    }
}

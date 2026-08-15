using System.Text.Json;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Store.Services;

public sealed partial class CartLine : ObservableObject
{
    public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isSwiped;
    public bool AllowsFractional { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public string? ImageKey { get; set; }

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public sealed class CartStore
{
    private const string Key = "cart_draft";

    public List<CartLine> Lines { get; } = [];
    public long? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string Note { get; private set; } = "";
    public string? SubmittedCartCode { get; private set; }
    public string? SubmissionIdempotencyKey { get; private set; }
    public string? CheckoutIdempotencyKey { get; private set; }
    public List<CartParticipantDraft> Participants { get; } = [];
    private CancellationTokenSource? _persistCts;

    public event Action? Changed;

    public int Count => Lines.Count;
    public decimal Total => Lines.Sum(l => l.LineTotal);

    public CartStore()
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var draft = JsonSerializer.Deserialize<Draft>(json);
            if (draft is null) return;
            Lines.AddRange(draft.Lines.Select(l => new CartLine
            {
                VariantId = l.VariantId,
                ProductName = l.ProductName,
                UnitName = l.UnitName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                AllowsFractional = l.AllowsFractional,
                ImageKey = l.ImageKey
            }));
            CustomerId = draft.CustomerId;
            CustomerName = draft.CustomerName;
            Note = draft.Note ?? "";
            SubmittedCartCode = draft.SubmittedCartCode;
            SubmissionIdempotencyKey = draft.SubmissionIdempotencyKey;
            CheckoutIdempotencyKey = draft.CheckoutIdempotencyKey;
            Participants.AddRange(draft.Participants ?? []);
        }
        catch
        {
            Preferences.Remove(Key);
        }
    }

    public void Add(ProductLookupDto product) => Add(product, product.PackQty > 0 ? product.PackQty : 1);

    public void Add(ProductLookupDto product, decimal quantity)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == product.VariantId);
        if (line is null)
            Lines.Add(new CartLine
            {
                VariantId = product.VariantId,
                ProductName = product.ProductName,
                UnitName = product.UnitName,
                UnitPrice = product.SellingPrice,
                Quantity = quantity,
                AllowsFractional = product.AllowsFractional,
                ImageKey = product.ImageKey
            });
        else
            line.Quantity += quantity;
        Save();
    }

    public bool SetQuantity(long variantId, decimal quantity)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == variantId);
        if (line is null || !QuantityInput.IsValid(quantity, line.AllowsFractional))
            return false;
        line.Quantity = quantity;
        Save(debouncePersistence: true);
        return true;
    }

    public void Remove(long variantId)
    {
        Lines.RemoveAll(l => l.VariantId == variantId);
        Save();
    }

    public void SetCustomer(long? id, string? name)
    {
        CustomerId = id;
        CustomerName = name;
        Save();
    }

    public void SetNote(string note)
    {
        Note = note;
        Save();
    }

    public void SetParticipant(long roleDefinitionId, long partyId, string partyName, string roleLabel)
    {
        Participants.RemoveAll(x => x.RoleDefinitionId == roleDefinitionId);
        Participants.Add(new CartParticipantDraft(roleDefinitionId, partyId, partyName, roleLabel));
        Save();
    }

    public bool AddParticipant(
        long roleDefinitionId,
        long partyId,
        string partyName,
        string roleLabel,
        int maxCount)
    {
        if (Participants.Any(x => x.RoleDefinitionId == roleDefinitionId && x.PartyId == partyId))
            return true;

        var existing = Participants.Where(x => x.RoleDefinitionId == roleDefinitionId).ToList();
        if (maxCount <= 1)
            Participants.RemoveAll(x => x.RoleDefinitionId == roleDefinitionId);
        else if (existing.Count >= maxCount)
            return false;

        Participants.Add(new CartParticipantDraft(roleDefinitionId, partyId, partyName, roleLabel));
        Save();
        return true;
    }

    public void RemoveParticipant(long roleDefinitionId, long partyId)
    {
        if (Participants.RemoveAll(x => x.RoleDefinitionId == roleDefinitionId && x.PartyId == partyId) > 0)
            Save();
    }

    public void ClearParticipant(long roleDefinitionId)
    {
        if (Participants.RemoveAll(x => x.RoleDefinitionId == roleDefinitionId) > 0)
            Save();
    }

    public void Clear()
    {
        var pending = Interlocked.Exchange(ref _persistCts, null);
        pending?.Cancel();
        pending?.Dispose();
        Lines.Clear();
        CustomerId = null;
        CustomerName = null;
        Note = "";
        Participants.Clear();
        Preferences.Remove(Key);
        Changed?.Invoke();
    }

    public void Restore(IEnumerable<CartLine> lines)
    {
        Lines.Clear();
        Lines.AddRange(lines);
        Save();
        Changed?.Invoke();
    }

    public string EnsureSubmissionIdempotencyKey()
    {
        SubmissionIdempotencyKey ??= Guid.NewGuid().ToString("N");
        Save(false);
        return SubmissionIdempotencyKey;
    }

    public string EnsureCheckoutIdempotencyKey()
    {
        CheckoutIdempotencyKey ??= Guid.NewGuid().ToString("N");
        Save(false);
        return CheckoutIdempotencyKey;
    }

    public void MarkSubmitted(string code)
    {
        SubmittedCartCode = code;
        Save(false);
    }

    private void Save(bool invalidateSubmission = true, bool debouncePersistence = false)
    {
        if (invalidateSubmission)
        {
            SubmittedCartCode = null;
            SubmissionIdempotencyKey = null;
            CheckoutIdempotencyKey = null;
        }
        Changed?.Invoke();
        if (debouncePersistence)
        {
            SchedulePersistence();
            return;
        }
        PersistNow();
    }

    private void PersistNow()
    {
        var pending = Interlocked.Exchange(ref _persistCts, null);
        pending?.Cancel();
        pending?.Dispose();
        var draft = new Draft(
            Lines.Select(l => new DraftLine(l.VariantId, l.ProductName, l.UnitName, l.UnitPrice, l.Quantity, l.ImageKey,
                l.AllowsFractional)).ToList(),
            CustomerId, CustomerName, Note, SubmittedCartCode, SubmissionIdempotencyKey, CheckoutIdempotencyKey,
            Participants.ToList());
        Preferences.Set(Key, JsonSerializer.Serialize(draft));
    }

    private void SchedulePersistence()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _persistCts, next);
        previous?.Cancel();
        previous?.Dispose();
        _ = PersistAfterQuietPeriodAsync(next);
    }

    private async Task PersistAfterQuietPeriodAsync(CancellationTokenSource owner)
    {
        try
        {
            await Task.Delay(140, owner.Token);
            if (ReferenceEquals(_persistCts, owner))
                PersistNow();
        }
        catch (OperationCanceledException)
        {
            // A newer quantity change superseded this write.
        }
    }

    private sealed record DraftLine(
        long VariantId,
        string ProductName,
        string UnitName,
        decimal UnitPrice,
        decimal Quantity,
        string? ImageKey,
        bool AllowsFractional = false);
    private sealed record Draft(
        List<DraftLine> Lines,
        long? CustomerId,
        string? CustomerName,
        string? Note,
        string? SubmittedCartCode = null,
        string? SubmissionIdempotencyKey = null,
        string? CheckoutIdempotencyKey = null,
        List<CartParticipantDraft>? Participants = null);
}

public sealed record CartParticipantDraft(long RoleDefinitionId, long PartyId, string PartyName, string RoleLabel);

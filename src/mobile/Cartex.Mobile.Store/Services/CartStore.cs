using System.Text.Json;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Prepacks;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Store.Services;

public sealed partial class CartLine : ObservableObject
{
    public long VariantId { get; set; }
    public long? PrepackId { get; set; }
    public bool IsPrepack => PrepackId is not null;
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal OriginalPrice { get; set; }
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isSwiped;
    [ObservableProperty] private bool _isExpanded;
    public bool AllowsFractional { get; set; }
    public bool AllowsAmountEntry { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public decimal? PriceOverride => UnitPrice != OriginalPrice ? UnitPrice : null;
    public string? ImageKey { get; set; }

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(PriceOverride));
    }
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

    // To'lov sahifasidan chiqib qaytilganda kiritilgan hamma narsa joyida turadi:
    // desktopdagi kabi savat butun savdo holatini saqlaydi, faqat qatorlarni emas.
    public CheckoutDraft Checkout { get; private set; } = new();
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
                OriginalPrice = l.OriginalPrice > 0 ? l.OriginalPrice : l.UnitPrice,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                AllowsFractional = l.AllowsFractional,
                AllowsAmountEntry = l.AllowsAmountEntry,
                ImageKey = l.ImageKey,
                PrepackId = l.PrepackId
            }));
            CustomerId = draft.CustomerId;
            CustomerName = draft.CustomerName;
            Note = draft.Note ?? "";
            SubmittedCartCode = draft.SubmittedCartCode;
            SubmissionIdempotencyKey = draft.SubmissionIdempotencyKey;
            CheckoutIdempotencyKey = draft.CheckoutIdempotencyKey;
            Participants.AddRange(draft.Participants ?? []);
            Checkout = draft.Checkout ?? new CheckoutDraft();
        }
        catch
        {
            Preferences.Remove(Key);
        }
    }

    public void Add(ProductLookupDto product) => Add(product, product.PackQty > 0 ? product.PackQty : 1);

    public void Add(ProductLookupDto product, decimal quantity)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == product.VariantId && !l.IsPrepack);
        if (line is null)
            Lines.Add(new CartLine
            {
                VariantId = product.VariantId,
                ProductName = product.ProductName,
                UnitName = product.UnitName,
                OriginalPrice = product.SellingPrice,
                UnitPrice = product.SellingPrice,
                Quantity = quantity,
                AllowsFractional = product.AllowsFractional,
                AllowsAmountEntry = product.AllowsAmountEntry,
                ImageKey = product.ImageKey
            });
        else
            line.Quantity += quantity;
        Save();
    }

    public bool AddPrepack(PrepackLookupDto prepack)
    {
        if (Lines.Any(l => l.PrepackId == prepack.PrepackId))
            return false;
        Lines.Add(new CartLine
        {
            VariantId = prepack.VariantId,
            PrepackId = prepack.PrepackId,
            ProductName = $"{prepack.ProductName} ({prepack.Quantity:0.###} {prepack.UnitName})",
            UnitName = prepack.UnitName,
            OriginalPrice = prepack.UnitPrice,
            UnitPrice = prepack.UnitPrice,
            Quantity = prepack.Quantity
        });
        Save();
        return true;
    }

    public bool SetQuantity(CartLine line, decimal quantity)
    {
        if (line.IsPrepack || !QuantityInput.IsValid(quantity, line.AllowsFractional))
            return false;
        line.Quantity = quantity;
        Save(debouncePersistence: true);
        return true;
    }

    // Mijoz "20 ming so'mlik" desa, miqdor summadan hisoblanadi. Pastga yaxlitlanadi:
    // ortiqcha berish do'kon zarari, kam berish esa mijoz o'zi ko'radigan farq.
    public decimal? SetAmount(CartLine line, decimal amount)
    {
        if (line.IsPrepack || !line.AllowsAmountEntry || line.UnitPrice <= 0 || amount <= 0)
            return null;
        var step = line.AllowsFractional ? 0.001m : 1m;
        var quantity = Math.Floor(amount / line.UnitPrice / step) * step;
        if (quantity <= 0) return null;
        line.Quantity = quantity;
        Save(debouncePersistence: true);
        return quantity;
    }

    public bool SetPrice(CartLine line, decimal price)
    {
        if (line.IsPrepack || price < 0)
            return false;
        line.UnitPrice = price;
        Save(debouncePersistence: true);
        return true;
    }

    // Savdoni tuzatish: bekor qilingan savdoning qatorlari savatga qaytariladi va kassir
    // xatoni tuzatib qayta yakunlaydi. Narx savdodagi holida qoladi.
    public void Restore(
        IEnumerable<CartLine> lines,
        long? customerId,
        string? customerName,
        string? note,
        CheckoutDraft? checkout = null)
    {
        Clear();
        Lines.AddRange(lines);
        CustomerId = customerId;
        CustomerName = customerName;
        Note = note ?? "";
        Checkout = checkout ?? new CheckoutDraft();
        Save();
    }

    public void Remove(CartLine line)
    {
        Lines.Remove(line);
        Save();
    }

    public void SetCustomer(long? id, string? name)
    {
        CustomerId = id;
        CustomerName = name;
        Save();
    }

    // To'lov holatini yozish savatning o'zini o'zgartirmaydi: yuborilgan savat kodi va
    // idempotentlik kaliti saqlanadi, aks holda har bosilgan raqam qayta yuborish
    // himoyasini nolga tushirardi.
    public void SetCheckout(CheckoutDraft checkout)
    {
        Checkout = checkout;
        Save(invalidateSubmission: false, debouncePersistence: true);
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
        Debounce.Cancel(ref _persistCts);
        Lines.Clear();
        CustomerId = null;
        CustomerName = null;
        Note = "";
        Checkout = new CheckoutDraft();
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
        Debounce.Cancel(ref _persistCts);
        var draft = new Draft(
            Lines.Select(l => new DraftLine(l.VariantId, l.ProductName, l.UnitName, l.UnitPrice, l.Quantity, l.ImageKey,
                l.AllowsFractional, l.OriginalPrice, l.AllowsAmountEntry, l.PrepackId)).ToList(),
            Checkout,
            CustomerId, CustomerName, Note, SubmittedCartCode, SubmissionIdempotencyKey, CheckoutIdempotencyKey,
            Participants.ToList());
        Preferences.Set(Key, JsonSerializer.Serialize(draft));
    }

    private void SchedulePersistence()
    {
        _ = PersistAfterQuietPeriodAsync(Debounce.Restart(ref _persistCts));
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
        bool AllowsFractional = false,
        decimal OriginalPrice = 0,
        bool AllowsAmountEntry = false,
        long? PrepackId = null);
    private sealed record Draft(
        List<DraftLine> Lines,
        CheckoutDraft? Checkout,
        long? CustomerId,
        string? CustomerName,
        string? Note,
        string? SubmittedCartCode = null,
        string? SubmissionIdempotencyKey = null,
        string? CheckoutIdempotencyKey = null,
        List<CartParticipantDraft>? Participants = null);
}

public sealed record CartParticipantDraft(long RoleDefinitionId, long PartyId, string PartyName, string RoleLabel);

// To'lov sahifasida kiritilgan hamma narsa: sahifadan chiqib qaytilganda ham, savdoni
// tuzatishda ham shu holat qaytariladi. Matn emas, son saqlanadi - format tilga bog'liq.
public sealed record CheckoutDraft(
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0,
    decimal Discount = 0,
    bool KeepExcessAsCredit = false,
    bool UseCustomerAdvance = true,
    string? DebtCurrency = null,
    DateTime? DebtDueDate = null,
    List<CheckoutPaymentDraft>? Payments = null);

public sealed record CheckoutPaymentDraft(string Method, string Currency, decimal Amount);

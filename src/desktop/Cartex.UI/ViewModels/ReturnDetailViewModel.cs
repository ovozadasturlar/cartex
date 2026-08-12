using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class ReturnDetailViewModel : ViewModelBase, IDialogContext
{
    private readonly PrintDispatchService _print;
    private readonly IToastService _toast;
    private readonly BranchContextService _branch;

    public ReturnDetailViewModel(
        CustomerReturnDocumentDto document,
        PrintDispatchService print,
        IToastService toast,
        BranchContextService branch)
    {
        Document = document;
        _print = print;
        _toast = toast;
        _branch = branch;
    }

    public CustomerReturnDocumentDto Document { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Document.Note);
    public bool HasCustomer => !string.IsNullOrWhiteSpace(Document.CustomerName);

    [RelayCommand]
    private async Task PrintAsync()
    {
        try
        {
            await _print.PrintReturnAsync(Document.Id, _branch.CurrentBranchId);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseDialog() => RequestClose?.Invoke(this, true);

    public void Close() => RequestClose?.Invoke(this, true);
}

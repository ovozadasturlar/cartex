using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

public partial class PromptViewModel(string title, string? message, string? placeholder, bool required = true)
    : ViewModelBase, IDialogContext
{
    public string Title { get; } = title;
    public string? Message { get; } = message;
    public string? Placeholder { get; } = placeholder;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    [ObservableProperty] private string _text = "";

    public bool CanSubmit => !required || !string.IsNullOrWhiteSpace(Text);

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit) return;
        RequestClose?.Invoke(this, Text.Trim());
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}

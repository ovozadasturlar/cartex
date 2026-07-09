using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Shared.Models.Common;

namespace Cartex.UI.ViewModels.Common;

public record SortOption(string Label, string Column)
{
    public override string ToString() => Label;
}

public partial class PaginationState : ObservableObject
{
    private Func<Task>? _reload;
    private bool _suppress;

    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = 30;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string? _sortBy;
    [ObservableProperty] private bool _descending;
    [ObservableProperty] private SortOption? _selectedSort;

    public IReadOnlyList<int> PageSizes { get; } = [20, 30, 50, 80];
    public IReadOnlyList<SortOption> SortOptions { get; private set; } = [];
    public bool HasSort => SortOptions.Count > 0;

    public bool CanPrev => Page > 1;
    public bool CanNext => Page < TotalPages;
    public string PageInfo => $"{Page} / {Math.Max(1, TotalPages)}";

    public void Attach(Func<Task> reload) => _reload = reload;

    public void ConfigureSort(IReadOnlyList<SortOption> options, SortOption? initial = null)
    {
        SortOptions = options;
        OnPropertyChanged(nameof(SortOptions));
        OnPropertyChanged(nameof(HasSort));
        _suppress = true;
        SelectedSort = initial ?? options.FirstOrDefault();
        SortBy = SelectedSort?.Column;
        _suppress = false;
    }

    public void Apply(PagedListMetadata meta)
    {
        TotalCount = meta.TotalCount;
        TotalPages = Math.Max(1, meta.TotalPages);
        if (Page > TotalPages) { _page = TotalPages; OnPropertyChanged(nameof(Page)); }
    }

    private Task Reload() => _reload?.Invoke() ?? Task.CompletedTask;

    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanPrev));
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(PageInfo));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanPrev));
        OnPropertyChanged(nameof(CanNext));
        OnPropertyChanged(nameof(PageInfo));
    }

    partial void OnPageSizeChanged(int value)
    {
        if (_suppress) return;
        if (Page != 1) { _page = 1; OnPropertyChanged(nameof(Page)); }
        _ = Reload();
    }

    partial void OnSelectedSortChanged(SortOption? value)
    {
        if (_suppress) return;
        SortBy = value?.Column;
        _page = 1; OnPropertyChanged(nameof(Page));
        _ = Reload();
    }

    [RelayCommand]
    private Task ToggleDirection()
    {
        Descending = !Descending;
        _page = 1; OnPropertyChanged(nameof(Page));
        return Reload();
    }

    [RelayCommand]
    private Task First() { if (!CanPrev) return Task.CompletedTask; Page = 1; return Reload(); }

    [RelayCommand]
    private Task Prev() { if (!CanPrev) return Task.CompletedTask; Page--; return Reload(); }

    [RelayCommand]
    private Task Next() { if (!CanNext) return Task.CompletedTask; Page++; return Reload(); }

    [RelayCommand]
    private Task Last() { if (!CanNext) return Task.CompletedTask; Page = TotalPages; return Reload(); }
}

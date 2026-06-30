using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsHubViewModel : ViewModelBase, ILoadable
{
    private readonly AuthService _authService;

    [ObservableProperty] private ViewModelBase? _currentSection;
    [ObservableProperty] private MenuItem? _selectedSection;

    public ObservableCollection<MenuSection> Sections { get; } = [];

    public SettingsHubViewModel(AuthService authService)
    {
        _authService = authService;
        LocalizationManager.Instance.LanguageChanged += RefreshTitles;
    }

    public Task LoadAsync()
    {
        Build();
        SelectedSection = Sections.SelectMany(s => s.Items).FirstOrDefault();
        return Task.CompletedTask;
    }

    private void Build()
    {
        Sections.Clear();
        foreach (var (key, titleKey) in NavRegistry.SettingsSections)
        {
            var section = new MenuSection { Key = key, Title = L[titleKey] };
            foreach (var def in NavRegistry.SettingsPages.Where(d => d.SectionKey == key))
            {
                if (def.Permission is not null && !_authService.HasPermission(def.Permission))
                    continue;
                section.Items.Add(new MenuItem
                {
                    Key = def.Key,
                    Icon = def.Icon,
                    ViewModelType = def.VmType,
                    Permission = def.Permission,
                    Title = L[def.Key]
                });
            }
            if (section.Items.Count > 0)
                Sections.Add(section);
        }
    }

    partial void OnSelectedSectionChanged(MenuItem? oldValue, MenuItem? newValue)
    {
        if (oldValue is not null) oldValue.IsActive = false;
        if (newValue is null) return;
        newValue.IsActive = true;
        CurrentSection = (ViewModelBase)ServiceLocator.Resolve(newValue.ViewModelType);
        if (CurrentSection is ILoadable loadable)
            _ = loadable.LoadAsync();
    }

    private void RefreshTitles()
    {
        foreach (var section in Sections)
        {
            section.Title = L[NavRegistry.SettingsSections.First(s => s.Key == section.Key).TitleKey];
            foreach (var item in section.Items)
                item.Title = L[item.Key];
        }
    }

    [RelayCommand]
    private void SelectSection(MenuItem item) => SelectedSection = item;

    public void SelectByKey(string key)
    {
        var item = Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Key == key);
        if (item is not null) SelectedSection = item;
    }
}

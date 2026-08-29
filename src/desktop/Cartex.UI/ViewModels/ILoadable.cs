namespace Cartex.UI.ViewModels;

public enum PageLoadState
{
    NotLoaded,
    Loading,
    Loaded,
    Failed
}

public interface ILoadable
{
    PageLoadState LoadState { get; }
    Task LoadAsync();
}

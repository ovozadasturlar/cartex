using Avalonia.Threading;
using Cartex.ApiClient;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public interface IBusyService
{
    bool IsBusy { get; }
    bool IsPageBusy { get; }
    string? Message { get; }
    IDisposable Begin(string? message = null);
}

/// <summary>
/// Ikki xil kutish holati:
/// <list type="bullet">
/// <item>Sahifa ma'lumotlarini yuklash — yengil chiziqli indikator. Foydalanuvchi kutmaydi: darhol
/// forma ochishi yoki boshqa sahifaga o'tishi mumkin, o'tganda so'rov bekor qilinadi.</item>
/// <item>Amal (saqlash, o'chirish, to'lov) — ekran ustidagi kutish kartochkasi.</item>
/// </list>
/// Turi avtomatik aniqlanadi: sahifa yuklash oqimida <see cref="PageRequestScope.IsPageLoad"/> true bo'ladi.
/// </summary>
public partial class BusyService : ObservableObject, IBusyService
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPageBusy;
    [ObservableProperty] private string? _message;
    private int _count;
    private int _pageCount;
    private DispatcherTimer? _showTimer;

    public IDisposable Begin(string? message = null)
    {
        if (PageRequestScope.IsPageLoad)
        {
            _pageCount++;
            IsPageBusy = true;
            return new Scope(this, page: true);
        }

        _count++;
        Message = message;
        if (_count == 1 && !IsBusy)
        {
            _showTimer?.Stop();
            _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _showTimer.Tick += (_, _) =>
            {
                _showTimer?.Stop();
                if (_count > 0) IsBusy = true;
            };
            _showTimer.Start();
        }
        return new Scope(this, page: false);
    }

    private void End(bool page)
    {
        if (page)
        {
            if (--_pageCount > 0) return;
            _pageCount = 0;
            IsPageBusy = false;
            return;
        }

        if (--_count > 0) return;
        _count = 0;
        _showTimer?.Stop();
        _showTimer = null;
        IsBusy = false;
        Message = null;
    }

    private sealed class Scope(BusyService owner, bool page) : IDisposable
    {
        public void Dispose() => owner.End(page);
    }
}

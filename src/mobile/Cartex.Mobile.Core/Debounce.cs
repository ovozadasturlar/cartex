using System.Diagnostics.CodeAnalysis;

namespace Cartex.Mobile.Core;

// Debounce/takror naqshlarida bekor qilish sinxron bo'lishi shart: chaqiruvchilar property
// setter, konstruktor va event handler'lar — ularni async qilib bo'lmaydi. Tokenlarda faqat
// Task.Delay davomlari turadi, shuning uchun CancelAsync hech narsa qo'shmaydi.
public static class Debounce
{
    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Sinxron bekor qilish ataylab: chaqiruvchilar setter/ctor, davomlar faqat Task.Delay.")]
    public static void Cancel(ref CancellationTokenSource? source)
    {
        var pending = Interlocked.Exchange(ref source, null);
        pending?.Cancel();
        pending?.Dispose();
    }

    public static CancellationTokenSource Restart(ref CancellationTokenSource? source)
    {
        Cancel(ref source);
        var next = new CancellationTokenSource();
        source = next;
        return next;
    }
}

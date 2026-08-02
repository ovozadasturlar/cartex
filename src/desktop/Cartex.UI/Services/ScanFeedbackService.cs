namespace Cartex.UI.Services;

public interface IScanFeedbackService
{
    void Error();
}

public sealed class ScanFeedbackService : IScanFeedbackService
{
    public void Error()
    {
        _ = Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    Console.Beep(900, 75);
                    if (i < 2) Thread.Sleep(45);
                }
            }
            catch
            {
            }
        });
    }
}

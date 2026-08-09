using System.Windows.Input;

namespace Cartex.Mobile.Core.Controls;

/// <summary>
/// Executes once on press, then repeats with acceleration while the pointer is held.
/// RepeatCommand is intentionally separate from Button.Command so release does not
/// execute the action a second time.
/// </summary>
public sealed class HoldRepeatButton : Button
{
    public static readonly BindableProperty RepeatCommandProperty = BindableProperty.Create(
        nameof(RepeatCommand), typeof(ICommand), typeof(HoldRepeatButton));

    public static readonly BindableProperty RepeatCommandParameterProperty = BindableProperty.Create(
        nameof(RepeatCommandParameter), typeof(object), typeof(HoldRepeatButton));

    public static readonly BindableProperty InitialDelayProperty = BindableProperty.Create(
        nameof(InitialDelay), typeof(int), typeof(HoldRepeatButton), 400);

    public ICommand? RepeatCommand
    {
        get => (ICommand?)GetValue(RepeatCommandProperty);
        set => SetValue(RepeatCommandProperty, value);
    }

    public object? RepeatCommandParameter
    {
        get => GetValue(RepeatCommandParameterProperty);
        set => SetValue(RepeatCommandParameterProperty, value);
    }

    public int InitialDelay
    {
        get => (int)GetValue(InitialDelayProperty);
        set => SetValue(InitialDelayProperty, value);
    }

    private CancellationTokenSource? _repeatCts;

    public HoldRepeatButton()
    {
        Pressed += OnPressed;
        Released += OnReleased;
    }

    protected override void OnHandlerChanged()
    {
        if (Handler is null)
            StopRepeating();
        base.OnHandlerChanged();
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        StopRepeating();
        Execute();

        var cts = _repeatCts = new CancellationTokenSource();
        _ = RepeatAsync(cts.Token);
    }

    private void OnReleased(object? sender, EventArgs e) => StopRepeating();

    private async Task RepeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Math.Max(250, InitialDelay), cancellationToken);
            var repeats = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!IsEnabled)
                    return;

                Execute();
                repeats++;

                // Deliberately bounded: fast enough for 1 -> 50, but still
                // controllable on inventory/financial input screens.
                var interval = repeats switch
                {
                    < 5 => 180,
                    < 14 => 105,
                    _ => 55
                };
                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal release/cancel path.
        }
    }

    private void Execute()
    {
        var command = RepeatCommand;
        var parameter = RepeatCommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    private void StopRepeating()
    {
        var cts = Interlocked.Exchange(ref _repeatCts, null);
        if (cts is null)
            return;
        cts.Cancel();
        cts.Dispose();
    }
}

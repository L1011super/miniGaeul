using GaeulDesktopPet.Models;

namespace GaeulDesktopPet.Services;

public sealed class RandomActionScheduler : IDisposable
{
    private CancellationTokenSource? _cts;
    public event Action? Due;

    public TimeSpan? NextDelay(PetSettings settings)
    {
        settings.Validate();
        return settings.InteractionFrequency switch
        {
            InteractionFrequencyLevel.Off => null,
            InteractionFrequencyLevel.Occasional => TimeSpan.FromMinutes(20),
            InteractionFrequencyLevel.Often => TimeSpan.FromMinutes(5),
            InteractionFrequencyLevel.Frequent => TimeSpan.FromMinutes(1),
            InteractionFrequencyLevel.Continuous => TimeSpan.FromSeconds(4),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    public void Restart(PetSettings settings)
    {
        Stop();
        if (NextDelay(settings) is null) return;
        _cts = new CancellationTokenSource();
        _ = RunAsync(settings, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(PetSettings settings, CancellationToken token)
    {
        try
        {
            var delay = NextDelay(settings);
            if (delay is null) return;
            await Task.Delay(delay.Value, token);
            if (!token.IsCancellationRequested) Due?.Invoke();
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => Stop();
}

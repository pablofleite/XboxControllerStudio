using XboxControllerStudio.Models;

namespace XboxControllerStudio.Services;

/// <summary>
/// Polls all four XInput slots on a background thread and raises
/// StateUpdated for each controller every poll cycle.
///
/// The polling interval targets ~8 ms (≈125 Hz), which is well below
/// XInput's own 125 Hz hardware report rate, so we never miss a frame.
///
/// Threading: StateUpdated is raised on the background thread.
/// Subscribers must marshal to the UI thread themselves (e.g. via
/// Application.Current.Dispatcher.InvokeAsync).
/// </summary>
public sealed class InputPollingService : IDisposable
{
    private const int PollingIntervalMs = 8;
    private const int MaxControllers = 4;

    private readonly XInputService _xinput;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pollingTask;

    /// <summary>
    /// Fired every polling cycle for each controller slot.
    /// playerIndex is 0-based.
    /// </summary>
    public event Action<ControllerState>? StateUpdated;

    public InputPollingService(XInputService xinput)
    {
        _xinput = xinput;
        // Start the loop as a long-running task so the thread pool
        // does not treat it as a short-lived work item.
        _pollingTask = Task.Factory.StartNew(
            PollLoopAsync,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task PollLoopAsync()
    {
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            for (int i = 0; i < MaxControllers; i++)
            {
                ControllerState state;
                try
                {
                    state = _xinput.GetState(i);
                }
                catch
                {
                    // Keep polling alive even if one slot fails transiently.
                    state = ControllerState.Disconnected(i);
                }

                StateUpdated?.Invoke(state);
            }

            // Yield the thread for the remainder of the interval,
            // keeping CPU usage negligible.
            await Task.Delay(PollingIntervalMs, token).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _pollingTask.Wait(500); } catch { /* ignore cancellation / timeout */ }
        _cts.Dispose();
    }
}

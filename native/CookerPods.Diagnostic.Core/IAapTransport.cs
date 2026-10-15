namespace CookerPods.Diagnostic.Core;

public interface IAapTransport : IAsyncDisposable
{
    ValueTask ConnectAsync(CancellationToken cancellationToken);
    ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}

// Owns one explicitly chosen driver's channel. Writes are serialized; receive
// reports can never be replaced by optimistic local toggle state.
public sealed class AapController(IAapTransport transport, TimeProvider? timeProvider = null) : IAsyncDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim lifecycle = new(1, 1), write = new(1, 1);
    private CancellationTokenSource? running;
    private Task? reader;
    private long epoch;
    private bool disposed;
    public AapControlState State { get; } = new(timeProvider);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (running is not null)
            {
                if (State.Snapshot().Phase is ControlPhase.Disconnected or ControlPhase.Failed)
                    throw new InvalidOperationException("Create a new session after disconnection.");
                return;
            }
            epoch = State.BeginConnection();
            running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(running.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                await transport.ConnectAsync(deadline.Token);
                State.ChannelOpened(epoch);
                await transport.SendAsync(AapProtocol.Handshake(), deadline.Token);
                await transport.SendAsync(AapProtocol.EnableFeatures(), deadline.Token);
                await transport.SendAsync(AapProtocol.Subscribe(), deadline.Token);
                reader = ReadLoop(epoch, running.Token);
            }
            catch (Exception error)
            {
                State.Disconnect(epoch, DiagnosticIssue.From("aap-connect", error));
                running.Cancel(); running.Dispose(); running = null;
                await transport.DisposeAsync();
                throw;
            }
        }
        finally { lifecycle.Release(); }
    }

    private async Task ReadLoop(long generation, CancellationToken cancellationToken)
    {
        long openedAt = clock.GetTimestamp();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = await transport.ReceiveAsync(cancellationToken);
                if (!packet.IsEmpty) State.Observe(generation, packet.Span);
                else await Task.Delay(TimeSpan.FromMilliseconds(30), cancellationToken);
                if (State.Snapshot().Phase == ControlPhase.AwaitingReports && clock.GetElapsedTime(openedAt) >= TimeSpan.FromSeconds(10))
                    throw new TimeoutException("No recognized AAP status arrived after initialization.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            State.Disconnect(generation, DiagnosticIssue.From("aap-receive", error));
            // Caller must explicitly reconnect; never replay pending writes.
            if (running is not null) await running.CancelAsync();
            await transport.DisposeAsync();
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                State.Disconnect(generation);
                await transport.DisposeAsync();
            }
        }
    }

    public Task SetModeAsync(NoiseMode mode, CancellationToken cancellationToken = default) =>
        ChangeAsync(mode, null, false, cancellationToken);
    public Task SetSettingAsync(AapSetting setting, bool enabled, CancellationToken cancellationToken = default) =>
        ChangeAsync(null, setting, enabled, cancellationToken);

    private async Task ChangeAsync(NoiseMode? mode, AapSetting? setting, bool enabled, CancellationToken cancellationToken)
    {
        await write.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var lifetime = running;
            long generation = epoch;
            if (lifetime is null || lifetime.IsCancellationRequested) throw new InvalidOperationException("Control session is disconnected.");
            byte[] packet;
            bool accepted = mode.HasValue ? State.TryRequestMode(generation, mode.Value, out packet)
                : State.TryRequestSetting(generation, setting!.Value, enabled, out packet);
            if (!accepted) throw new InvalidOperationException("The requested setting is unknown, unchanged, or busy.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(4));
            try { await transport.SendAsync(packet, deadline.Token); }
            catch (Exception error)
            {
                State.WriteFailed(generation, DiagnosticIssue.From("aap-send", error));
                throw;
            }
        }
        finally { write.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            State.Disconnect(epoch);
            if (running is not null) await running.CancelAsync();
            await write.WaitAsync();
            try { await transport.DisposeAsync(); }
            finally { write.Release(); }
            if (reader is not null) await reader;
            running?.Dispose(); running = null;
        }
        finally { lifecycle.Release(); }
    }
}

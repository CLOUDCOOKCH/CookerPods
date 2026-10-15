using CookerPods.Diagnostic.Core;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace CookerPods.Diagnostic.Windows;

internal interface IMonitorRadio : IDisposable
{
    void Start();
    void Pause();
}

internal sealed class MonitorRadio(LiveMonitorSession session) : IMonitorRadio
{
    private BluetoothLEAdvertisementWatcher? watcher;
    private global::Windows.Foundation.TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs>? received;
    private global::Windows.Foundation.TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementWatcherStoppedEventArgs>? stopped;

    public void Start()
    {
        Pause();
        long epoch = session.BeginScan();
        try
        {
            watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Passive };
            received = (sender, args) =>
            {
                try
                {
                    foreach (var section in args.Advertisement.ManufacturerData)
                    {
                        if (section.CompanyId != 0x004c) continue;
                        if (section.Data.Length > 512)
                        {
                            session.Observe(epoch, args.BluetoothAddress, new byte[513]);
                            continue;
                        }
                        byte[] payload = new byte[section.Data.Length];
                        using var reader = DataReader.FromBuffer(section.Data);
                        reader.ReadBytes(payload);
                        session.Observe(epoch, args.BluetoothAddress, payload);
                    }
                }
                catch (Exception error)
                {
                    session.ScanStopped(epoch, DiagnosticIssue.From("monitor-receive", error));
                    try { sender.Stop(); } catch { }
                }
            };
            stopped = (_, args) => session.ScanStopped(epoch,
                new("monitor-watcher", "WatcherStopped", args.Error.ToString()));
            watcher.Received += received;
            watcher.Stopped += stopped;
            watcher.Start();
        }
        catch (Exception error)
        {
            session.ScanStopped(epoch, DiagnosticIssue.From("monitor-start", error));
            Detach();
        }
    }

    public void Pause()
    {
        // Invalidate the generation before stopping the OS source. Queued events
        // from that source cannot update readings or stop a later source.
        session.Pause();
        Detach();
    }

    private void Detach()
    {
        var old = watcher;
        watcher = null;
        if (old is null) return;
        if (received is not null) old.Received -= received;
        if (stopped is not null) old.Stopped -= stopped;
        received = null;
        stopped = null;
        try { old.Stop(); } catch { /* The session already records its stopped/failed state. */ }
    }

    public void Dispose() => Pause();
}

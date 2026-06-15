using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using HPToy.Core.Objects;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using HPToy.Core.Device;
using HPToy.Core.Numbers;

namespace HPToy.Core.Ble;

public enum BleConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    ConnectionReady
}

public sealed class BleClient : IBleTransport
{
    public static BleClient Instance { get; } = new();

    public static string LogFilePath => Path.Combine(Path.GetTempPath(), "hptoy_ble.log");

    /// <summary>Set from the WPF app so BLE events and write callbacks run on the UI thread.</summary>
    public Action<Action>? PostToUi { get; set; }

    private static readonly Guid Fff0ServiceId = Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb");
    private static readonly Guid Fff1Id = Guid.Parse("0000fff1-0000-1000-8000-00805f9b34fb");
    private static readonly Guid Fff2Id = Guid.Parse("0000fff2-0000-1000-8000-00805f9b34fb");
    private static readonly Guid Fff3Id = Guid.Parse("0000fff3-0000-1000-8000-00805f9b34fb");

    private const short Cc2540PageSize = 2048;
    private const short AttachPageOffset = 3 * Cc2540PageSize;

    private readonly BlePacketQueue _packets = new();
    private BluetoothLEDevice? _device;
    private GattSession? _session;
    private GattCharacteristic? _fff1;
    private GattCharacteristic? _fff2;
    private GattCharacteristic? _fff3;
    private bool _bleBusy;
    private BlePacket? _inFlight;
    private readonly PeripheralData _pd = new();
    private DateTime _suppressDisconnectUntil = DateTime.MinValue;
    private volatile bool _expectingInitDspDisconnect;
    private volatile bool _gattRestoreNeeded;
    private readonly SemaphoreSlim _gattRestoreLock = new(1, 1);
    private ulong _connectedAddress;
    private string? _connectedDeviceId;
    private readonly List<Action> _writePostProcesses = new();
    private readonly List<(IPostProcess? Post, Action Send)> _pendingFlashExports = new();
    private readonly List<Action<byte[]>> _paramDataHandlers = new();
    private volatile bool _flashTransferActive;
    private Action<short>? _checksumQueryCallback;
    private bool _silentChecksumRead;
    private short _lastComparedDeviceChecksum;
    private HiFiToyDevice? _handshakeDevice;
    private Action? _afterBleIdleAction;
    private readonly Queue<Action> _afterBleIdleActions = new();
    private readonly BlockingCollection<Action> _bleQueue = new();
    private readonly Thread _bleWorker;

    private BleClient()
    {
        _bleWorker = new Thread(BleWorkerLoop)
        {
            Name = "HPToy.BleWorker",
            IsBackground = true
        };
        _bleWorker.Start();
    }

    // Inter-write pacing (0 = full speed). Tunable at runtime via WriteThrottleMs.
    public static int WriteThrottleMs { get; set; } = 0;

    public BleConnectionState State { get; private set; } = BleConnectionState.Disconnected;
    public bool IsConnected => State >= BleConnectionState.Connected;
    public bool IsConnectionReady => State == BleConnectionState.ConnectionReady;
    public bool IsFlashTransferActive => _flashTransferActive;

    public event Action<BleConnectionState>? StateChanged;
    public event Action? FlashTransferRejected;
    public event Action<string>? Log;
    public event Action? PairingRequired;
    public event Action<short, short>? PresetChecksumMismatch;
    public event Action<bool>? ClipFlagChanged;
    public event Action<int>? WriteProgress;
    public event Action? WriteCompleted;
    public event Action<byte[]>? ParamDataReceived;
    /// <summary>Energy / advertise / output mode were read from device header.</summary>
    public event Action? DeviceSettingsImported;

    public Task ConnectAsync(ulong bluetoothAddress, HiFiToyDevice device, string? deviceId = null) =>
        RunOnBleThreadAsync(() => ConnectAsyncCore(bluetoothAddress, device, deviceId));

    public Task DisconnectAsync() => RunOnBleThreadAsync(DisconnectCoreAsync);

    /// <summary>
    /// Queues a flash write (Export / InitDsp). Rejects overlapping requests so rapid
    /// double-saves cannot interleave InitDsp + GATT restore.
    /// </summary>
    public void EnqueueFlashExport(IPostProcess? postProcess, Action sendPackets, bool notifyIfRejected = true)
    {
        RunOnBleThread(() =>
        {
            if (!TryBeginFlashTransfer())
            {
                if (notifyIfRejected)
                    PostUi(() => FlashTransferRejected?.Invoke());
                else
                    _pendingFlashExports.Add((postProcess, sendPackets));
                return;
            }

            if (postProcess != null)
                _writePostProcesses.Add(() => postProcess.OnPostProcess());
            sendPackets();
        });
    }

    private void ProcessPendingFlashExports()
    {
        if (_pendingFlashExports.Count == 0)
            return;

        if (!TryBeginFlashTransfer())
            return;

        var (post, send) = _pendingFlashExports[0];
        _pendingFlashExports.RemoveAt(0);
        if (post != null)
            _writePostProcesses.Add(() => post.OnPostProcess());
        send();
    }

    private bool TryBeginFlashTransfer()
    {
        if (_flashTransferActive || _bleBusy || _inFlight != null || _packets.Count > 0)
            return false;
        _flashTransferActive = true;
        return true;
    }

    private void BleWorkerLoop()
    {
        SynchronizationContext.SetSynchronizationContext(new BleThreadSyncContext(this));
        foreach (var work in _bleQueue.GetConsumingEnumerable())
            work();
    }

    private void RunOnBleThread(Action action)
    {
        if (Thread.CurrentThread == _bleWorker)
            action();
        else
            _bleQueue.Add(action);
    }

    private Task RunOnBleThreadAsync(Func<Task> action)
    {
        if (Thread.CurrentThread == _bleWorker)
            return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _bleQueue.Add(() => RunQueuedAsync(action, tcs));
        return tcs.Task;
    }

    private async void RunQueuedAsync(Func<Task> action, TaskCompletionSource tcs)
    {
        try
        {
            await action();
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    private void PostUi(Action action)
    {
        if (PostToUi != null)
            PostToUi(action);
        else
            action();
    }

    private void RaiseStateChanged()
    {
        var state = State;
        PostUi(() => StateChanged?.Invoke(state));
    }

    private void RaiseLog(string message)
    {
        PostUi(() => Log?.Invoke(message));
    }

    private void RaiseWriteProgress(int count) =>
        PostUi(() => WriteProgress?.Invoke(count));

    private void RaiseWriteCompleted()
    {
        _flashTransferActive = false;
        var posts = _writePostProcesses.ToArray();
        _writePostProcesses.Clear();
        PostUi(() =>
        {
            WriteCompleted?.Invoke();
            foreach (var post in posts)
                post();
        });
        ProcessPendingFlashExports();
    }

    private void RaisePairingRequired() => PostUi(() => PairingRequired?.Invoke());

    private void RaisePresetChecksumMismatch(short deviceCs, short appCs) =>
        PostUi(() => PresetChecksumMismatch?.Invoke(deviceCs, appCs));

    private void RaiseClipFlagChanged(bool clipped) =>
        PostUi(() => ClipFlagChanged?.Invoke(clipped));

    private void RaiseDeviceSettingsImported() =>
        PostUi(() => DeviceSettingsImported?.Invoke());

    public void SubscribeParamData(Action<byte[]> handler) =>
        RunOnBleThread(() => _paramDataHandlers.Add(handler));

    public void UnsubscribeParamData(Action<byte[]> handler) =>
        RunOnBleThread(() => _paramDataHandlers.Remove(handler));

    private void ClearParamDataHandlers() => _paramDataHandlers.Clear();

    private void DeliverParamData(byte[] data)
    {
        foreach (var handler in _paramDataHandlers.ToArray())
            handler(data);
        ParamDataReceived?.Invoke(data);
    }

    /// <summary>Read-only checksum query while already connected (no handshake / mismatch UI).</summary>
    public void QueryDeviceChecksum(Action<short> onResult)
    {
        RunOnBleThread(() =>
        {
            _silentChecksumRead = true;
            _checksumQueryCallback = onResult;
            GetChecksumParamData();
        });
    }

    public void NotifyUi(Action action) => PostUi(action);

    public void ScheduleAfterBleIdle(Action action)
    {
        RunOnBleThread(() =>
        {
            _afterBleIdleActions.Enqueue(action);
            TryRunAfterBleIdle();
        });
    }

    /// <summary>True when no GATT writes are queued or in flight (energy sync completion probe).</summary>
    public bool IsWriteQueueIdle =>
        !_flashTransferActive && !_bleBusy && _inFlight == null && _packets.Count == 0;

    public short LastComparedDeviceChecksum => _lastComparedDeviceChecksum;

    /// <summary>Import from peripheral data cached during the connect handshake.</summary>
    /// <returns>null on success, error message on failure.</returns>
    public void ImportPresetFromCache(Func<PeripheralData, string?> applyImport)
    {
        RunOnBleThread(() =>
        {
            if (_pd.IsPresetCached())
            {
                EmitLog("使用连接缓存的预设数据…");
                ApplyImportResult(applyImport(_pd.Clone()));
                return;
            }

            if (_pd.HasParsedHeader())
                ImportPresetBodyFromCache(applyImport);
            else
                _ = ImportPresetFromCacheAsync(applyImport);
        });
    }

    private void ApplyImportResult(string? err)
    {
        if (err == null)
            EmitLog("预设已导入。");
        else
            EmitLog($"预设导入失败: {err}");
    }

    private void ImportPresetBodyFromCache(Func<PeripheralData, string?> applyImport)
    {
        EmitLog("正在从设备读取预设（头已缓存）…");
        _pd.ImportPresetBody(new PostAction(() =>
        {
            if (!_pd.IsPresetCached())
            {
                EmitLog("预设读取失败。");
                return;
            }

            ApplyImportResult(applyImport(_pd.Clone()));
        }));
    }

    private async Task ImportPresetFromCacheAsync(Func<PeripheralData, string?> applyImport)
    {
        EmitLog("正在从设备读取预设…");
        var importPd = new PeripheralData();
        importPd.Import(new PostAction(() =>
        {
            if (!importPd.IsPresetCached())
            {
                EmitLog("预设读取失败。");
                return;
            }

            var err = applyImport(importPd.Clone());
            ApplyImportResult(err);
        }));
    }

    internal PeripheralData GetCachedPeripheralData() => _pd.Clone();

    private sealed class BleThreadSyncContext : SynchronizationContext
    {
        private readonly BleClient _client;

        public BleThreadSyncContext(BleClient client) => _client = client;

        public override void Post(SendOrPostCallback d, object? state) =>
            _client.RunOnBleThread(() => d(state));
    }

    private async Task ConnectAsyncCore(
        ulong bluetoothAddress,
        HiFiToyDevice device,
        string? deviceId)
    {
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await ConnectCoreAsync(bluetoothAddress, device, deviceId, connectCts.Token)
                .WaitAsync(connectCts.Token);
        }
        catch (OperationCanceledException)
        {
            const string msg = "连接超时。请确认设备已开机且在范围内，并检查 Windows 蓝牙已开启。";
            EmitLog(msg);
            await DisconnectCoreAsync();
            throw new TimeoutException(msg);
        }
    }

    private async Task ConnectCoreAsync(
        ulong bluetoothAddress,
        HiFiToyDevice device,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        await DisconnectCoreAsync();
        await Task.Delay(1000, cancellationToken);
        State = BleConnectionState.Connecting;
        RaiseStateChanged();
        EmitLog("正在连接…");

        try
        {
            device.SetMac(FormatMac(bluetoothAddress));
            HiFiToyDeviceManager.Instance.SetActiveDevice(device);
            _handshakeDevice = device;
            _connectedAddress = bluetoothAddress;
            _connectedDeviceId = deviceId;
            _gattRestoreNeeded = false;
            _pd.Clear();
            ClearParamDataHandlers();

            var services = await OpenAndDiscoverServicesAsync(bluetoothAddress, deviceId, cancellationToken);

            EmitLog("读取特征…");
            await ResolveFffCharacteristicsAsync(services, cancellationToken);

            _fff2!.ValueChanged += OnCharacteristicValueChanged;
            _fff3!.ValueChanged += OnCharacteristicValueChanged;

            State = BleConnectionState.Connected;
            RaiseStateChanged();
            EmitLog("启用通知…");

            await EnableNotificationsAsync(_fff2, cancellationToken);
            await EnableNotificationsAsync(_fff3, cancellationToken);

            EmitLog("应用握手…");
            StartPairedProcess(device.GetPairingCode());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            EmitLog($"连接失败: {ex.Message}");
            await DisconnectCoreAsync();
            throw;
        }
    }

    private async Task<IReadOnlyList<GattDeviceService>> OpenAndDiscoverServicesAsync(
        ulong bluetoothAddress,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        EmitLog("打开设备…");
        var device = await OpenDeviceAsync(bluetoothAddress, deviceId, cancellationToken);
        if (device == null)
            throw new InvalidOperationException("无法打开蓝牙设备句柄（设备可能已关机或超出范围）。");

        _device = device;
        _device.ConnectionStatusChanged += OnConnectionStatusChanged;

        // 不在每次连接时自动取消配对——第二次连接常因此找不到 FFF 特征。

        // 用长生命周期的 _session 字段持有 GattSession，让 Windows 主动维持链路。
        // 注意：GattSession 必须保活，否则被 GC 回收后 MaintainConnection 立即失效。
        try
        {
            _session = await BleAsync.WithTimeout(
                GattSession.FromDeviceIdAsync(device.BluetoothDeviceId),
                TimeSpan.FromSeconds(5),
                "建立会话");
            _session.MaintainConnection = true;
            EmitLog("MaintainConnection 已启用");
            await Task.Delay(250, cancellationToken);
        }
        catch (Exception ex)
        {
            EmitLog($"会话警告: {ex.Message}");
        }

        EmitLog("建立 GATT…");

        const int maxAttempts = 60;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt == 1)
                EmitLog($"等待 BLE 链路… (当前={device.ConnectionStatus})");
            else if (attempt % 10 == 0)
                LogBleOnly($"GATT 尝试 {attempt}/{maxAttempts}… (链路={device.ConnectionStatus})");

            if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                await Task.Delay(200, cancellationToken);
                continue;
            }

            try
            {
                var result = await GetGattServicesAsync(device, BluetoothCacheMode.Cached, cancellationToken);
                if (result?.Status == GattCommunicationStatus.Success &&
                    result.Services.Count > 0 &&
                    ServicesIncludeFffService(result.Services))
                {
                    EmitLog($"GATT 已连接（{result.Services.Count} 个服务）");
                    return result.Services;
                }

                result = await GetGattServicesAsync(device, BluetoothCacheMode.Uncached, cancellationToken);
                if (result?.Status == GattCommunicationStatus.Success &&
                    result.Services.Count > 0 &&
                    ServicesIncludeFffService(result.Services))
                {
                    EmitLog($"GATT 已连接（{result.Services.Count} 个服务）");
                    return result.Services;
                }
            }
            catch (TimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (attempt % 15 == 0)
            {
                EmitLog("刷新设备句柄…");
                try
                {
                    device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    device.Dispose();
                }
                catch
                {
                }

                if (_session != null)
                {
                    try
                    {
                        _session.MaintainConnection = false;
                        _session.Dispose();
                    }
                    catch
                    {
                    }

                    _session = null;
                }

                device = await OpenDeviceAsync(bluetoothAddress, deviceId, cancellationToken);
                if (device == null)
                    throw new InvalidOperationException("无法重新打开蓝牙设备。");

                _device = device;
                device.ConnectionStatusChanged += OnConnectionStatusChanged;

                try
                {
                    _session = await BleAsync.WithTimeout(
                        GattSession.FromDeviceIdAsync(device.BluetoothDeviceId),
                        TimeSpan.FromSeconds(5),
                        "建立会话");
                    _session.MaintainConnection = true;
                    await Task.Delay(250, cancellationToken);
                }
                catch
                {
                }
            }

            await Task.Delay(150, cancellationToken);
        }

        throw new InvalidOperationException(
            "GATT 连接超时，未找到 FFF0 服务。请确认设备已开机并在范围内后重试。");
    }

    private static async Task<GattDeviceServicesResult?> GetGattServicesAsync(
        BluetoothLEDevice device,
        BluetoothCacheMode cacheMode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await BleAsync.WithTimeout(
                device.GetGattServicesAsync(cacheMode),
                TimeSpan.FromSeconds(5),
                "GATT 服务");
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private static bool ServicesIncludeFffService(IReadOnlyList<GattDeviceService> services) =>
        services.Any(s => s.Uuid == Fff0ServiceId);

    private async Task CleanStalePairingAsync(BluetoothLEDevice device)
    {
        try
        {
            var pairing = device.DeviceInformation.Pairing;
            if (!pairing.IsPaired)
                return;

            EmitLog("清除残留配对记录…");
            var result = await BleAsync.WithTimeout(
                pairing.UnpairAsync(), TimeSpan.FromSeconds(8), "取消配对");
            EmitLog($"取消配对: {result.Status}");
        }
        catch (Exception ex)
        {
            EmitLog($"取消配对异常: {ex.Message}");
        }
    }

    private static async Task<BluetoothLEDevice?> OpenDeviceAsync(
        ulong bluetoothAddress,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        BluetoothLEDevice? device = null;

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            device = await BleAsync.WithTimeout(
                BluetoothLEDevice.FromIdAsync(deviceId), TimeSpan.FromSeconds(5), "打开设备");
        }

        device ??= await BleAsync.WithTimeout(
            BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress),
            TimeSpan.FromSeconds(5),
            "打开设备");

        if (device != null)
            return device;

        foreach (var id in BuildBleDeviceIds(bluetoothAddress))
        {
            cancellationToken.ThrowIfCancellationRequested();
            device = await BleAsync.WithTimeout(
                BluetoothLEDevice.FromIdAsync(id), TimeSpan.FromSeconds(3), "打开设备");
            if (device != null)
                break;
        }

        return device;
    }

    private async Task ResolveFffCharacteristicsAsync(
        IReadOnlyList<GattDeviceService> services,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 25;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _fff1 = null;
            _fff2 = null;
            _fff3 = null;

            var fff0 = await GetFff0ServiceAsync(cancellationToken);
            if (fff0 != null)
                await TryReadFffFromServiceAsync(fff0, cancellationToken);

            if (_fff1 == null || _fff2 == null || _fff3 == null)
                await ScanAllServicesForFffAsync(services, cancellationToken);

            if (_fff1 != null && _fff2 != null && _fff3 != null)
                return;

            if (attempt >= maxAttempts || _device == null)
                break;

            LogBleOnly($"特征重试 {attempt}/{maxAttempts}…");
            // After InitDsp / full flash export the CC2540 may expose FFF0 before FFF1–3.
            var retryDelay = fff0 != null && attempt <= 5 ? 800 : attempt <= 3 ? 200 : 350;
            await Task.Delay(retryDelay, cancellationToken);

            DisposeGattServices(services);

            if (attempt % 5 == 0 && _connectedAddress != 0)
            {
                LogBleOnly("刷新设备句柄（特征发现）…");
                try
                {
                    _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _device.Dispose();
                }
                catch
                {
                }

                var refreshed = await OpenDeviceAsync(
                    _connectedAddress, _connectedDeviceId, cancellationToken);
                if (refreshed != null)
                {
                    _device = refreshed;
                    _device.ConnectionStatusChanged += OnConnectionStatusChanged;
                    try
                    {
                        _session?.Dispose();
                        _session = await BleAsync.WithTimeout(
                            GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId),
                            TimeSpan.FromSeconds(5),
                            "建立会话");
                        _session.MaintainConnection = true;
                        await Task.Delay(250, cancellationToken);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    _device = null;
                }
            }

            if (_device == null)
                break;

            var refresh = await BleAsync.WithTimeout(
                _device.GetGattServicesAsync(BluetoothCacheMode.Uncached),
                TimeSpan.FromSeconds(5),
                "刷新 GATT 服务");

            if (refresh.Status == GattCommunicationStatus.Success && refresh.Services.Count > 0)
                services = refresh.Services;
        }

        var svcList = string.Join(", ", services.Select(s => s.Uuid.ToString()));
        EmitLog($"已发现服务: {svcList}");
        throw new InvalidOperationException(
            "未找到 FFF1/FFF2/FFF3 特征。请确认连接的是 PowerDAC（PDV21Peripheral）。");
    }

    private async Task<GattDeviceService?> GetFff0ServiceAsync(CancellationToken cancellationToken)
    {
        if (_device == null)
            return null;

        foreach (var cacheMode in new[] { BluetoothCacheMode.Cached, BluetoothCacheMode.Uncached })
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var byUuid = await BleAsync.WithTimeout(
                    _device.GetGattServicesForUuidAsync(Fff0ServiceId, cacheMode),
                    TimeSpan.FromSeconds(5),
                    "FFF0 服务");
                if (byUuid.Status == GattCommunicationStatus.Success && byUuid.Services.Count > 0)
                    return byUuid.Services[0];
            }
            catch (TimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return null;
    }

    private static void DisposeGattServices(IReadOnlyList<GattDeviceService> services)
    {
        foreach (var service in services)
        {
            try
            {
                service.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task TryReadFffFromServiceAsync(
        GattDeviceService service,
        CancellationToken cancellationToken)
    {
        await AssignFffCharacteristicsByUuidAsync(service, BluetoothCacheMode.Cached, cancellationToken);
        if (_fff1 != null && _fff2 != null && _fff3 != null)
            return;

        await TryAssignFffFromAllCharacteristicsAsync(service, BluetoothCacheMode.Uncached, cancellationToken);
        if (_fff1 != null && _fff2 != null && _fff3 != null)
            return;

        for (var i = 0; i < 4; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await AssignFffCharacteristicsByUuidAsync(service, BluetoothCacheMode.Uncached, cancellationToken);
            if (_fff1 != null && _fff2 != null && _fff3 != null)
                return;

            await TryAssignFffFromAllCharacteristicsAsync(service, BluetoothCacheMode.Uncached, cancellationToken);
            if (_fff1 != null && _fff2 != null && _fff3 != null)
                return;

            await Task.Delay(150, cancellationToken);
        }
    }

    private async Task TryAssignFffFromAllCharacteristicsAsync(
        GattDeviceService service,
        BluetoothCacheMode cacheMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var chars = await BleAsync.WithTimeout(
                service.GetCharacteristicsAsync(cacheMode),
                TimeSpan.FromSeconds(3),
                "读取特征");
            if (chars.Status == GattCommunicationStatus.Success)
                AssignFffCharacteristics(chars.Characteristics);
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task AssignFffCharacteristicsByUuidAsync(
        GattDeviceService service,
        BluetoothCacheMode cacheMode,
        CancellationToken cancellationToken)
    {
        if (_fff1 == null)
            await TryAssignCharacteristicByUuidAsync(service, Fff1Id, ch => _fff1 = ch, cancellationToken, cacheMode);
        if (_fff2 == null)
            await TryAssignCharacteristicByUuidAsync(service, Fff2Id, ch => _fff2 = ch, cancellationToken, cacheMode);
        if (_fff3 == null)
            await TryAssignCharacteristicByUuidAsync(service, Fff3Id, ch => _fff3 = ch, cancellationToken, cacheMode);
    }

    private static async Task TryAssignCharacteristicByUuidAsync(
        GattDeviceService service,
        Guid characteristicId,
        Action<GattCharacteristic> assign,
        CancellationToken cancellationToken,
        BluetoothCacheMode cacheMode = BluetoothCacheMode.Cached)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await BleAsync.WithTimeout(
                service.GetCharacteristicsForUuidAsync(characteristicId, cacheMode),
                TimeSpan.FromSeconds(3),
                "读取特征");
            if (result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0)
                assign(result.Characteristics[0]);
        }
        catch (TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task ScanAllServicesForFffAsync(
        IReadOnlyList<GattDeviceService> services,
        CancellationToken cancellationToken)
    {
        foreach (var service in services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var chars = await BleAsync.WithTimeout(
                    service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached),
                    TimeSpan.FromSeconds(3),
                    "读取特征");
                if (chars.Status == GattCommunicationStatus.Success)
                    AssignFffCharacteristics(chars.Characteristics);
            }
            catch (TimeoutException)
            {
            }
        }
    }

    private void AssignFffCharacteristics(IReadOnlyList<GattCharacteristic> characteristics)
    {
        foreach (var ch in characteristics)
        {
            if (ch.Uuid == Fff1Id) _fff1 = ch;
            else if (ch.Uuid == Fff2Id) _fff2 = ch;
            else if (ch.Uuid == Fff3Id) _fff3 = ch;
        }
    }

    private async Task ResolveCharacteristicsAsync(
        IReadOnlyList<GattDeviceService> services,
        CancellationToken cancellationToken)
    {
        await ResolveFffCharacteristicsAsync(services, cancellationToken);
    }

    private async Task EnableNotificationsAsync(GattCharacteristic ch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = await BleAsync.WithTimeout(
            ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify),
            TimeSpan.FromSeconds(5),
            "启用通知");
        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"启用通知失败 ({status})");
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args) =>
        RunOnBleThread(() => OnConnectionStatusChangedCore(sender));

    private void OnConnectionStatusChangedCore(BluetoothLEDevice sender)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            if (_expectingInitDspDisconnect)
                _expectingInitDspDisconnect = false;
            if (_gattRestoreNeeded)
                _ = TryRestoreGattSessionAsync(force: true, quiet: true);
            return;
        }

        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected
            && State != BleConnectionState.Disconnected
            && State != BleConnectionState.Connecting)
        {
            if (DateTime.UtcNow < _suppressDisconnectUntil && _expectingInitDspDisconnect)
            {
                _gattRestoreNeeded = true;
                EmitLog("InitDsp 后忽略短暂断开");
                return;
            }

            if (_bleBusy || _inFlight != null || _packets.Count > 0 || _flashTransferActive)
            {
                _gattRestoreNeeded = true;
                LogBleOnly("链路短暂抖动，正在恢复…");
                _ = TryRestoreGattSessionAsync(force: true, quiet: true);
                return;
            }

            EmitLog("设备已断开。");
            _ = DisconnectCoreAsync();
        }
    }

    private void MarkInitDspDisconnectExpected()
    {
        _suppressDisconnectUntil = DateTime.UtcNow.AddSeconds(8);
        _expectingInitDspDisconnect = true;
    }

    /// <summary>
    /// After InitDsp the link may flap; WinRT invalidates GATT characteristics even when
    /// we skip DisconnectAsync. Re-discover services before the next write.
    /// </summary>
    private async Task<bool> TryRestoreGattSessionAsync(bool force = false, bool quiet = false)
    {
        await _gattRestoreLock.WaitAsync();

        try
        {
            if (!force && !_gattRestoreNeeded)
                return _fff1 != null;

            if (_device == null && _connectedAddress != 0)
            {
                _device = await OpenDeviceAsync(_connectedAddress, _connectedDeviceId, CancellationToken.None);
                if (_device != null)
                    _device.ConnectionStatusChanged += OnConnectionStatusChanged;
            }

            if (_device == null)
                return false;

            for (var i = 0; i < 50 && _device.ConnectionStatus != BluetoothConnectionStatus.Connected; i++)
                await Task.Delay(200);

            if (_device == null || _device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                return false;

            if (_session == null)
            {
                try
                {
                    _session = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId);
                    _session.MaintainConnection = true;
                }
                catch (Exception ex)
                {
                    LogBleOnly($"会话恢复警告: {ex.Message}");
                }
            }

            try
            {
                if (_fff2 != null)
                    _fff2.ValueChanged -= OnCharacteristicValueChanged;
            }
            catch
            {
            }

            try
            {
                if (_fff3 != null)
                    _fff3.ValueChanged -= OnCharacteristicValueChanged;
            }
            catch
            {
            }

            var servicesResult = await BleAsync.WithTimeout(
                _device.GetGattServicesAsync(BluetoothCacheMode.Uncached),
                TimeSpan.FromSeconds(10),
                "恢复 GATT 服务");

            if (servicesResult.Status != GattCommunicationStatus.Success
                || servicesResult.Services.Count == 0)
                return false;

            try
            {
                await ResolveCharacteristicsAsync(servicesResult.Services, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (_fff2 == null || _fff3 == null || _fff1 == null)
                return false;

            _fff2.ValueChanged += OnCharacteristicValueChanged;
            _fff3.ValueChanged += OnCharacteristicValueChanged;
            await EnableNotificationsAsync(_fff2, CancellationToken.None);
            await EnableNotificationsAsync(_fff3, CancellationToken.None);

            _gattRestoreNeeded = false;
            if (!quiet)
                EmitLog("GATT 已恢复，可继续写入。");
            else
                LogBleOnly("InitDsp 后 GATT 已静默恢复。");
            return true;
        }
        catch (Exception ex)
        {
            EmitLog($"GATT 恢复失败: {ex.Message}");
            return false;
        }
        finally
        {
            _gattRestoreLock.Release();
        }
    }

    private static bool IsDisposedBleError(Exception ex) =>
        ex is ObjectDisposedException
        || ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase);

    private static bool IsCrossThreadBleError(Exception ex) =>
        ex.Message.Contains("另一个线程拥有", StringComparison.Ordinal)
        || ex.Message.Contains("another thread owns", StringComparison.OrdinalIgnoreCase);

    private static bool IsInitDspPacket(ReadOnlySpan<byte> data) =>
        data.Length == CommonCommand.Length && data[0] == CommonCommand.InitDsp;

    /// <summary>
    /// Await a GATT write. If the watchdog fires, drain the underlying WinRT
    /// operation before returning — abandoning an in-flight write leaves a second
    /// WriteValueAsync running and Windows disconnects the link.
    /// </summary>
    private static async Task<GattCommunicationStatus> WriteCharacteristicAsync(
        GattCharacteristic characteristic,
        byte[] data,
        GattWriteOption option,
        TimeSpan timeout)
    {
        var writeTask = characteristic.WriteValueAsync(data.AsBuffer(), option).AsTask();
        var completed = await Task.WhenAny(writeTask, Task.Delay(timeout));
        if (completed != writeTask)
        {
            try { await writeTask; } catch { /* drain orphan */ }
            throw new TimeoutException($"写入数据 超时 ({timeout.TotalSeconds:0}s)");
        }

        return await writeTask;
    }

    private async Task DisconnectCoreAsync()
    {
        _suppressDisconnectUntil = DateTime.MinValue;
        _expectingInitDspDisconnect = false;
        _flashTransferActive = false;
        _writePostProcesses.Clear();
        _pendingFlashExports.Clear();
        _gattRestoreNeeded = false;
        _connectedAddress = 0;
        _connectedDeviceId = null;
        _packets.Clear();
        _bleBusy = false;
        _inFlight = null;
        ClearParamDataHandlers();
        _checksumQueryCallback = null;
        _silentChecksumRead = false;
        _handshakeDevice = null;
        _afterBleIdleAction = null;
        _afterBleIdleActions.Clear();

        if (_fff2 != null)
        {
            _fff2.ValueChanged -= OnCharacteristicValueChanged;
            _fff2 = null;
        }

        if (_fff3 != null)
        {
            _fff3.ValueChanged -= OnCharacteristicValueChanged;
            _fff3 = null;
        }

        _fff1 = null;

        if (_session != null)
        {
            try
            {
                _session.MaintainConnection = false;
                _session.Dispose();
            }
            catch
            {
            }

            _session = null;
        }

        if (_device != null)
        {
            try
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _device.Dispose();
            }
            catch
            {
            }

            _device = null;
        }

        await Task.Delay(400);
        State = BleConnectionState.Disconnected;
        RaiseStateChanged();
        await Task.CompletedTask;
    }

    public void QueueEnergyWrite(byte[] data, Action? onIdle = null)
    {
        RunOnBleThread(() =>
        {
            _packets.Add(new BlePacket(data, true));
            if (!_bleBusy)
                WriteNextPacket();
            if (onIdle != null)
            {
                _afterBleIdleActions.Enqueue(onIdle);
                TryRunAfterBleIdle();
            }
        });
    }

    public void SendDataToDsp(byte[] data, bool response) =>
        SendDataToDsp(new BlePacket(data, response));

    public void SendDataToDsp(BlePacket packet)
    {
        RunOnBleThread(() =>
        {
            _packets.Add(packet);
            if (!_bleBusy)
                WriteNextPacket();
        });
    }

    private async void WriteNextPacket()
    {
        // One outstanding write at a time. The packet is pulled out of the queue
        // before sending so the dedup logic can never drop an in-flight packet.
        if (_inFlight != null)
            return;
        if (State == BleConnectionState.Disconnected)
        {
            _bleBusy = false;
            TryRunAfterBleIdle();
            return;
        }

        if (_gattRestoreNeeded)
            await TryRestoreGattSessionAsync(force: true, quiet: true);

        if (_fff1 == null)
        {
            _bleBusy = false;
            TryRunAfterBleIdle();
            return;
        }

        var packet = _packets.RemoveFirst();
        if (packet == null)
        {
            _bleBusy = false;
            TryRunAfterBleIdle();
            return;
        }

        _inFlight = packet;
        _bleBusy = true;
        try
        {
            // Pace writes so a burst doesn't overrun a weak link (cause of drops).
            if (WriteThrottleMs > 0)
                await Task.Delay(WriteThrottleMs);

            if (_fff1 == null || State == BleConnectionState.Disconnected)
            {
                _inFlight = null;
                _bleBusy = false;
                TryRunAfterBleIdle();
                return;
            }

            var isInitDsp = IsInitDspPacket(packet.Data);
            if (isInitDsp)
                MarkInitDspDisconnectExpected();

            // Let the CC2540 finish committing flash (WriteFlag=1) before InitDsp.
            // Peripheral config stores initDspDelay as (byte)32 → 32<<3 = 256ms on device.
            if (isInitDsp)
                await Task.Delay(PeripheralData.InitDspDelay << 3);

            if (_fff1 == null || State == BleConnectionState.Disconnected)
            {
                _inFlight = null;
                _bleBusy = false;
                return;
            }

            GattCommunicationStatus status;
            try
            {
                // Match Android: every FFF1 write uses with-response (serialized queue).
                // Always drain orphaned WinRT tasks on timeout (see WriteCharacteristicAsync).
                var timeout = isInitDsp ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(8);
                status = await WriteCharacteristicAsync(
                    _fff1, packet.Data, GattWriteOption.WriteWithResponse, timeout);
            }
            catch (Exception) when (isInitDsp)
            {
                // InitDsp may outlive the ATT window while the TAS5558 reloads; command
                // was still accepted once the write left our queue.
                status = GattCommunicationStatus.Success;
            }

            _inFlight = null;

            if (status != GattCommunicationStatus.Success && !isInitDsp)
            {
                AbortTransfer($"写入失败 ({status})");
                return;
            }

            OnCharacteristicWritten(packet);
        }
        catch (TimeoutException ex)
        {
            AbortTransfer(ex.Message);
        }
        catch (Exception ex) when (IsDisposedBleError(ex) || IsCrossThreadBleError(ex))
        {
            _inFlight = null;
            _gattRestoreNeeded = true;
            LogBleOnly("GATT 句柄失效，正在恢复…");
            if (await TryRestoreGattSessionAsync(force: true, quiet: true))
            {
                _packets.AddFirst(packet);
                _bleBusy = false;
                WriteNextPacket();
                return;
            }

            AbortTransfer($"Write error: {ex.Message}");
        }
        catch (Exception ex)
        {
            AbortTransfer($"Write error: {ex.Message}");
        }
    }

    private void OnCharacteristicWritten(BlePacket sent)
    {
        CheckOutputModeHwSupport(sent.Data);
        RaiseWriteProgress(_packets.Count);

        if (IsInitDspPacket(sent.Data))
        {
            _gattRestoreNeeded = true;
            _ = FinishAfterInitDspAsync();
            return;
        }

        ContinueWriteQueue();
    }

    private async Task FinishAfterInitDspAsync()
    {
        await Task.Delay(PeripheralData.InitDspDelay << 3);
        await TryRestoreGattSessionAsync(force: true, quiet: true);
        ContinueWriteQueue();
    }

    private void TryRunAfterBleIdle()
    {
        if (_flashTransferActive || _bleBusy || _inFlight != null || _packets.Count > 0)
            return;

        if (_afterBleIdleAction != null)
        {
            var legacy = _afterBleIdleAction;
            _afterBleIdleAction = null;
            legacy();
        }

        while (_afterBleIdleActions.Count > 0)
        {
            var action = _afterBleIdleActions.Dequeue();
            action();
        }
    }

    private void ContinueWriteQueue()
    {
        if (_packets.Count > 0)
            WriteNextPacket();
        else
        {
            _bleBusy = false;
            TryRunAfterBleIdle();
            RaiseWriteCompleted();
        }
    }

    private void AbortTransfer(string message)
    {
        _inFlight = null;
        _packets.Clear();
        _bleBusy = false;
        _flashTransferActive = false;
        _writePostProcesses.Clear();
        _pendingFlashExports.Clear();
        RaiseLog(message);
        TryRunAfterBleIdle();
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs e)
    {
        var data = e.CharacteristicValue?.ToArray();
        if (data != null)
            RunOnBleThread(() => HandleNotification(data));
    }

    private HiFiToyDevice SessionDevice() =>
        _handshakeDevice ?? HiFiToyDeviceManager.Instance.GetActiveDevice();

    private void HandleNotification(byte[] data)
    {
        if (data.Length == 13 && data[0] == CommonCommand.GetEnergyConfig)
        {
            SessionDevice().GetEnergyConfig().ParseBinary(data.AsSpan(1));
            return;
        }

        if (data.Length == 4)
            HandleFeedback(data);

        if (data.Length == 20)
            DeliverParamData(data);
    }

    private void CheckOutputModeHwSupport(byte[] data)
    {
        if (data.Length < 5) return;
        var cmd = data[0];
        if (cmd < CommonCommand.SetTas5558Ch3Mixer || cmd > CommonCommand.GetOutputMode) return;

        SessionDevice().GetOutputMode().SetHwSupported(true);
        HiFiToyDeviceManager.Instance.Store();
    }

    private void HandleFeedback(byte[] data)
    {
        var dev = SessionDevice();
        var feedback = data[0];
        var status = data[1];

        switch (feedback)
        {
            case CommonCommand.EstablishPair:
                if (status != 0) CheckWriteFlag();
                else RaisePairingRequired();
                break;

            case CommonCommand.GetWriteFlag:
                if (status != 0) GetVersion();
                else dev.RestoreFactorySettings(new PostAction(CheckWriteFlag));
                break;

            case CommonCommand.GetVersion:
                var version = data[1] | (data[2] << 8);
                if (version == dev.GetVersion()) dev.GetAudioSource().ReadFromDsp();
                else dev.RestoreFactorySettings(new PostAction(GetVersion));
                break;

            case CommonCommand.GetAudioSource:
                dev.GetAudioSource().SetSource(status);
                ReadAmModeThenImportHeader();
                break;

            case CommonCommand.GetAdvertiseMode:
                dev.GetAdvertiseMode().SetMode(status);
                break;

            case CommonCommand.ClipDetection:
                dev.SetClipFlag(status != 0);
                RaiseClipFlagChanged(status != 0);
                break;

            case CommonCommand.GetChecksum:
                var checksum = (short)(data[1] | (data[2] << 8));
                if (dev.GetAmMode().IsEnabled())
                {
                    var d0 = dev.GetAmMode().GetDataBufs()[0].GetBinary();
                    checksum = Checksummer.SubtractData(checksum, _pd.GetDataBufLength(), d0);
                }

                if (_silentChecksumRead)
                {
                    _silentChecksumRead = false;
                    var cb = _checksumQueryCallback;
                    _checksumQueryCallback = null;
                    var result = checksum;
                    PostUi(() => cb?.Invoke(result));
                    break;
                }

                _lastComparedDeviceChecksum = checksum;

                // Mark ready before any UI callback — ApplyHeader/Store must not run
                // before this, and must not use Dispatcher.Invoke (deadlocks the BLE thread).
                State = BleConnectionState.ConnectionReady;
                RaiseStateChanged();

                try
                {
                    _pd.ApplyHeaderToDevice(dev);
                    HiFiToyDeviceManager.Instance.Store();
                    RaiseDeviceSettingsImported();
                }
                catch (Exception ex)
                {
                    EmitLog($"同步设备设置失败: {ex.Message}");
                }

                var ack = dev.GetAckDeviceChecksum();
                if (ack != 0 && ack == checksum)
                {
                    EmitLog($"已连接。设备校验=0x{checksum:X4}, 应用=0x{checksum:X4}");
                    dev.SetAckDeviceChecksum(checksum);
                }
                else
                {
                    var appChecksum = dev.GetActivePreset().GetChecksum();
                    EmitLog($"已连接。设备校验=0x{checksum:X4}, 应用=0x{appChecksum:X4}");

                    if (appChecksum == checksum)
                        dev.SetAckDeviceChecksum(checksum);
                    else if (dev.GetActiveKeyPreset().Equals("No processing", StringComparison.Ordinal))
                    {
                        EmitLog($"已连接。设备校验=0x{checksum:X4}，写入 No processing…");
                        PushActivePresetToDevice(dev);
                    }
                    else
                        RaisePresetChecksumMismatch(checksum, appChecksum);
                }
                break;
        }
    }

    private void PushActivePresetToDevice(HiFiToyDevice dev)
    {
        EmitLog("写入 No processing 到 DSP…");
        dev.GetActivePreset().SendToPeripheral(true);
        ScheduleAfterBleIdle(() =>
        {
            QueryDeviceChecksum(cs =>
            {
                dev.SetAckDeviceChecksum(cs);
                EmitLog($"No processing 已应用，校验=0x{cs:X4}");
            });
        });
    }

    private void ReadAmModeThenImportHeader()
    {
        void OnAmData(byte[] data)
        {
            UnsubscribeParamData(OnAmData);
            SessionDevice().GetAmMode().ImportFromDataBufs(new List<HiFiToyDataBuf> { new HiFiToyDataBuf(data) });
            _pd.ImportHeader(new PostAction(GetChecksumParamData));
        }

        SubscribeParamData(OnAmData);
        GetDspDataWithOffset(PeripheralData.PeripheralConfigLength);
    }

    public void StartPairedProcess(int pairingCode)
    {
        var d = new byte[5];
        d[0] = CommonCommand.EstablishPair;
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(1), pairingCode);
        SendDataToDsp(d, true);
    }

    public void SendNewPairingCode(int pairingCode)
    {
        var d = new byte[5];
        d[0] = CommonCommand.SetPairCode;
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(1), pairingCode);
        SendDataToDsp(d, true);
    }

    private void CheckWriteFlag() =>
        SendDataToDsp([CommonCommand.GetWriteFlag, 0, 0, 0, 0], true);

    private void GetVersion() =>
        SendDataToDsp([CommonCommand.GetVersion, 0, 0, 0, 0], true);

    private void GetChecksumParamData() =>
        SendDataToDsp([CommonCommand.GetChecksum, 0, 0, 0, 0], true);

    public void GetDspDataWithOffset(short offset)
    {
        var b = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(b, offset);
        SendDataToDsp(b, true);
    }

    public void SendWriteFlag(byte writeFlag) =>
        SendDataToDsp([CommonCommand.SetWriteFlag, writeFlag, 0, 0, 0], true);

    public void SetInitDsp() =>
        SendDataToDsp([CommonCommand.InitDsp, 0, 0, 0, 0], true);

    public void SendBufToDsp(short offsetInDspData, byte[] data)
    {
        var offset = 0;
        var remaining = data.Length;
        var pageRemainder = Cc2540PageSize - offsetInDspData % Cc2540PageSize;
        var chunk = Math.Min(pageRemainder, remaining);

        while (offset < data.Length)
        {
            for (var i = 0; i < chunk; i += 16)
            {
                var slice = data.AsSpan(offset + i, Math.Min(16, chunk - i)).ToArray();
                Send16Bytes((short)((AttachPageOffset + i) >> 2), slice);
            }

            MoveAttachPageToDspData((short)(offsetInDspData + offset), (short)chunk);
            offset += chunk;
            remaining = data.Length - offset;
            chunk = Math.Min(remaining, Cc2540PageSize);
        }
    }

    private void Send16Bytes(short wordOffset, byte[] sixteenBytes)
    {
        if (sixteenBytes.Length != 16) return;
        var b = new byte[18];
        BinaryPrimitives.WriteInt16LittleEndian(b, wordOffset);
        sixteenBytes.CopyTo(b, 2);
        SendDataToDsp(b, true);
    }

    private void MoveAttachPageToDspData(short offset, short length)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(b, offset);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(2), length);
        SendDataToDsp(b, true);
    }

    public static string FormatMac(ulong address)
    {
        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
            bytes[5 - i] = (byte)((address >> (i * 8)) & 0xFF);
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    internal static string FormatBleDeviceId(ulong address)
    {
        var mac = FormatMac(address);
        var dashed = mac.Replace(':', '-');
        return $"BluetoothLE#BluetoothLE{dashed}-{dashed}";
    }

    internal static IEnumerable<string> BuildBleDeviceIds(ulong address)
    {
        yield return FormatBleDeviceId(address);

        var mac = FormatMac(address);
        var lower = mac.ToLowerInvariant();
        yield return $"BluetoothLE#BluetoothLE{lower}-{lower}";
        yield return $"BluetoothLE#BluetoothLE{mac}-{mac}";
    }

    private void EmitLog(string message)
    {
        RaiseLog(message);
        LogBleOnly(message);
    }

    private void LogBleOnly(string message)
    {
        try
        {
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch
        {
        }
    }
}

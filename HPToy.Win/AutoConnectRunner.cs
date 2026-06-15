using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HPToy.Core.Ble;
using HPToy.Core.Objects;
using HPToy.Win.Services;

namespace HPToy.Win;

/// <summary>Headless connect / reconnect verification (--autotest).</summary>
internal static class AutoConnectRunner
{
    public static string LogPath =>
        Path.Combine(Path.GetTempPath(), "hptoy_autotest.log");

    public static async Task<int> RunAsync()
    {
        var log = new StringBuilder();
        void L(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            log.AppendLine(line);
            try { File.AppendAllText(LogPath, line + Environment.NewLine); } catch { }
        }

        try { File.WriteAllText(LogPath, ""); } catch { }
        try { File.WriteAllText(BleClient.LogFilePath, ""); } catch { }
        L("=== HPToy 自动连接测试 ===");

        var mismatchEvents = 0;
        var flashRejected = 0;
        HPToyAppService.Instance.PresetChecksumMismatch += (_, _) => mismatchEvents++;
        HPToyAppService.Instance.FlashTransferRejected += () => flashRejected++;

        HPToyAppService.Instance.LogMessage += msg => L($"  {msg}");

        var device = await ScanDeviceAsync(TimeSpan.FromSeconds(15), L);
        if (device == null)
        {
            L("FAIL: 未发现 PDV21Peripheral / HPToyPeripheral");
            await FlushLog(log, 1);
            return 1;
        }

        L($"发现设备: {device.Name} @ {device.DisplayAddress}");

        mismatchEvents = 0;
        flashRejected = 0;
        if (!await TryConnectAsync("第一次连接", L, device))
        {
            await FlushLog(log, 2);
            return 2;
        }

        if (!await WaitForConnectChecksumAsync(TimeSpan.FromSeconds(45), L))
        {
            await FlushLog(log, 5);
            return 5;
        }

        L($"第一次连接后 mismatch={mismatchEvents}, flashRejected={flashRejected}");

        await Task.Delay(1500);
        if (!await TryEnergySyncAsync(L))
        {
            await FlushLog(log, 6);
            return 6;
        }

        if (!await TryRapidEnergySyncAsync(L))
        {
            await FlushLog(log, 15);
            return 15;
        }

        if (!await TryDoubleEnergySyncSequentialAsync(L))
        {
            await FlushLog(log, 16);
            return 16;
        }

        await Task.Delay(2500);

        if (!await TryAmStoreAsync(L))
        {
            await FlushLog(log, 11);
            return 11;
        }

        L($"拍音/削波后 flashRejected={flashRejected}");
        if (flashRejected > 0)
        {
            L("FAIL: 拍音/削波写入触发了“正在写入硬件”拒绝");
            await FlushLog(log, 12);
            return 12;
        }

        // Full preset flash + InitDsp needs longer settle before disconnect/reconnect.
        await Task.Delay(TimeSpan.FromSeconds(8));

        await HPToyAppService.Instance.DisconnectAsync();
        await Task.Delay(TimeSpan.FromSeconds(8));

        mismatchEvents = 0;
        flashRejected = 0;
        if (!await TryConnectAsync("第二次连接", L, device))
        {
            await FlushLog(log, 4);
            return 4;
        }

        if (!await WaitForConnectionReadyAsync(TimeSpan.FromSeconds(75), L))
        {
            await FlushLog(log, 13);
            return 13;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        L($"第二次连接后 mismatch={mismatchEvents}, flashRejected={flashRejected}");

        if (mismatchEvents > 0)
        {
            L("FAIL: 第二次连接仍触发预设不一致");
            await FlushLog(log, 3);
            return 3;
        }

        if (flashRejected > 0)
        {
            L("FAIL: 第二次连接触发了 flash 写入拒绝弹窗");
            await FlushLog(log, 14);
            return 14;
        }

        await HPToyAppService.Instance.DisconnectAsync();

        L("PASS: 两次连接 + 拍音/削波后无弹窗");
        await FlushLog(log, 0);
        return 0;
    }

    /// <summary>Connect once in a fresh process — checksums must match, no mismatch dialog.</summary>
    public static async Task<int> RunColdStartAsync()
    {
        var log = new StringBuilder();
        void L(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            log.AppendLine(line);
            try { File.AppendAllText(LogPath, line + Environment.NewLine); } catch { }
        }

        try { File.WriteAllText(LogPath, ""); } catch { }
        try { File.WriteAllText(BleClient.LogFilePath, ""); } catch { }
        L("=== HPToy 冷启动连接测试 ===");

        var mismatchEvents = 0;
        HPToyAppService.Instance.PresetChecksumMismatch += (_, _) => mismatchEvents++;

        HPToyAppService.Instance.LogMessage += msg => L($"  {msg}");

        var device = await ScanDeviceAsync(TimeSpan.FromSeconds(15), L);
        if (device == null)
        {
            L("FAIL: 未发现设备");
            await FlushLog(log, 7);
            return 7;
        }

        if (!await TryConnectAsync("冷启动连接", L, device))
        {
            await FlushLog(log, 8);
            return 8;
        }

        if (!await WaitForStrictChecksumMatchAsync(TimeSpan.FromSeconds(45), L))
        {
            await FlushLog(log, 9);
            return 9;
        }

        if (mismatchEvents > 0)
        {
            L("FAIL: 冷启动触发了预设不一致弹窗/事件");
            await FlushLog(log, 10);
            return 10;
        }

        try
        {
            if (File.Exists(BleClient.LogFilePath))
            {
                var ble = File.ReadAllText(BleClient.LogFilePath);
                if (ble.Contains("设备预设不一致") || ble.Contains("预设已导入"))
                {
                    L("FAIL: 冷启动不应触发导入流程");
                    await FlushLog(log, 11);
                    return 11;
                }
            }
        }
        catch
        {
        }

        L("PASS: 冷启动连接校验和一致，无导入");
        await HPToyAppService.Instance.DisconnectAsync();
        await FlushLog(log, 0);
        return 0;
    }

    private static async Task<BleDeviceInfo?> ScanDeviceAsync(TimeSpan timeout, Action<string> log)
    {
        var scanner = new BleScanner();
        var found = new TaskCompletionSource<BleDeviceInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        scanner.DeviceFound += info =>
        {
            if (BleScanner.SupportedNames.Contains(info.Name))
                found.TrySetResult(info);
        };
        scanner.ScanStatus += msg => log($"  扫描: {msg}");

        scanner.Start();
        var completed = await Task.WhenAny(found.Task, Task.Delay(timeout));
        scanner.Stop();
        scanner.Dispose();

        return completed == found.Task ? await found.Task : null;
    }

    private static async Task<bool> TryConnectAsync(
        string label,
        Action<string> log,
        BleDeviceInfo? knownDevice = null)
    {
        log($"{label}…");
        var device = knownDevice ?? await ScanDeviceAsync(TimeSpan.FromSeconds(12), log);
        if (device == null)
        {
            log($"{label} FAIL: 扫描不到设备");
            return false;
        }

        try
        {
            await HPToyAppService.Instance.ConnectAsync(device);
        }
        catch (Exception ex)
        {
            log($"{label} FAIL: {ex.Message}");
            return false;
        }

        if (!await WaitForConnectionReadyAsync(TimeSpan.FromSeconds(75), log, label))
            return false;

        log($"{label} OK — ConnectionReady");
        return true;
    }

    private static async Task<bool> WaitForConnectionReadyAsync(
        TimeSpan timeout,
        Action<string> log,
        string? label = null)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (HPToyAppService.Instance.IsConnectionReady)
                return true;

            if (BleClient.Instance.State == BleConnectionState.Disconnected)
            {
                log($"{label ?? "连接"} FAIL: 连接中途断开");
                return false;
            }

            await Task.Delay(400);
        }

        log($"{label ?? "连接"} FAIL: 超时未就绪 (state={BleClient.Instance.State})");
        return false;
    }

    private static async Task<bool> WaitForConnectChecksumAsync(TimeSpan timeout, Action<string> log)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        var importAttempted = false;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(BleClient.LogFilePath))
                {
                    var ble = File.ReadAllText(BleClient.LogFilePath);
                    if (ChecksumsMatchedInLog(ble))
                    {
                        log("设备与应用校验和一致");
                        return true;
                    }

                    if (ble.Contains("No processing 已应用"))
                    {
                        log("No processing 已应用到 DSP");
                        return true;
                    }

                    if (ble.Contains("预设已写入设备"))
                    {
                        log("No processing 已写入设备");
                        return true;
                    }

                    if (ble.Contains("预设已导入"))
                    {
                        log("预设导入完成（首次同步）");
                        return true;
                    }

                    if (!importAttempted && ChecksumMismatchInLog(ble) &&
                        !ble.Contains("写入 No processing") &&
                        !ble.Contains("No processing 已应用"))
                    {
                        importAttempted = true;
                        log("首次连接不一致，执行一次性导入…");
                        HPToyAppService.Instance.ImportPresetFromDevice();
                    }

                    if (ble.Contains("预设导入失败"))
                    {
                        var idx = ble.LastIndexOf("预设导入失败", StringComparison.Ordinal);
                        var snippet = ble.Substring(idx, Math.Min(200, ble.Length - idx));
                        log($"FAIL: {snippet.Replace('\n', ' ')}");
                        return false;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        log("FAIL: 未检测到校验和一致");
        return false;
    }

    private static async Task<bool> WaitForStrictChecksumMatchAsync(TimeSpan timeout, Action<string> log)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(BleClient.LogFilePath))
                {
                    var ble = File.ReadAllText(BleClient.LogFilePath);
                    if (ChecksumsMatchedInLog(ble))
                    {
                        log("设备与应用校验和一致");
                        return true;
                    }

                    if (ble.Contains("预设已导入") || ble.Contains("设备预设不一致"))
                    {
                        log("FAIL: 冷启动不应触发导入或预设不一致流程");
                        return false;
                    }

                    if (ble.Contains("预设导入失败"))
                    {
                        var idx = ble.LastIndexOf("预设导入失败", StringComparison.Ordinal);
                        var snippet = ble.Substring(idx, Math.Min(200, ble.Length - idx));
                        log($"FAIL: {snippet.Replace('\n', ' ')}");
                        return false;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        log("FAIL: 冷启动未检测到校验和一致");
        return false;
    }

    private static bool ChecksumMismatchInLog(string bleLog) =>
        Regex.IsMatch(
            bleLog,
            @"已连接。设备校验=0x([0-9A-F]{4}), 应用=0x([0-9A-F]{4})",
            RegexOptions.CultureInvariant) &&
        !ChecksumsMatchedInLog(bleLog);

    private static async Task<bool> TryEnergySyncAsync(Action<string> log)
    {
        log("削波/自动关机同步测试…");
        var dev = HPToyAppService.Instance.Device;
        var ec = dev.GetEnergyConfig();
        ec.SetHighThresholdDbPercent(0.5f);
        dev.StoreEnergyConfigToPeripheral(null);

        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            if (!HPToyAppService.Instance.IsConnectionReady)
            {
                log("FAIL: 削波同步后连接断开");
                return false;
            }

            try
            {
                if (File.Exists(BleClient.LogFilePath))
                {
                    var ble = File.ReadAllText(BleClient.LogFilePath);
                    if (ble.Contains("GATT 恢复失败"))
                    {
                        log("FAIL: 削波同步触发 GATT 恢复失败");
                        return false;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(300);
        }

        if (!HPToyAppService.Instance.IsConnectionReady)
        {
            log("FAIL: 削波同步后未保持连接");
            return false;
        }

        log("削波同步 OK");
        return true;
    }

    private static async Task<bool> TryRapidEnergySyncAsync(Action<string> log)
    {
        log("削波连续同步 3 次…");
        var dev = HPToyAppService.Instance.Device;
        var ec = dev.GetEnergyConfig();

        for (var i = 0; i < 3; i++)
        {
            ec.SetHighThresholdDbPercent(0.3f + i * 0.1f);
            dev.StoreEnergyConfigToPeripheral(null);
            await Task.Delay(150);
        }

        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            if (!HPToyAppService.Instance.IsConnectionReady)
            {
                log("FAIL: 连续削波同步后连接断开");
                return false;
            }

            if (!BleClient.Instance.IsConnected)
            {
                log("FAIL: 连续削波同步后未连接");
                return false;
            }

            try
            {
                if (File.Exists(BleClient.LogFilePath))
                {
                    var ble = File.ReadAllText(BleClient.LogFilePath);
                    if (ble.Contains("InitDsp 后忽略短暂断开"))
                    {
                        log("FAIL: 削波同步误触发 InitDsp 断开提示");
                        return false;
                    }
                }
            }
            catch
            {
            }

            if (!BleClient.Instance.IsFlashTransferActive &&
                BleClient.Instance.State == BleConnectionState.ConnectionReady)
            {
                log("连续削波同步 OK");
                return true;
            }

            await Task.Delay(200);
        }

        log("FAIL: 连续削波同步超时");
        return false;
    }

    private static async Task<bool> TryDoubleEnergySyncSequentialAsync(Action<string> log)
    {
        log("削波两次顺序同步（验证可重复）…");
        if (!await WaitForBleWriteIdleAsync(TimeSpan.FromSeconds(8), log))
        {
            log("FAIL: 削波同步前 BLE 未空闲");
            return false;
        }

        var dev = HPToyAppService.Instance.Device;

        dev.GetEnergyConfig().SetHighThresholdDbPercent(0.35f);
        dev.StoreEnergyConfigToPeripheral();
        if (!await WaitForBleWriteIdleAsync(TimeSpan.FromSeconds(12), log))
        {
            log("FAIL: 第一次削波同步未完成");
            return false;
        }

        dev.GetEnergyConfig().SetHighThresholdDbPercent(0.55f);
        dev.StoreEnergyConfigToPeripheral();
        if (!await WaitForBleWriteIdleAsync(TimeSpan.FromSeconds(12), log))
        {
            log("FAIL: 第二次削波同步未完成");
            return false;
        }

        if (!HPToyAppService.Instance.IsConnectionReady)
        {
            log("FAIL: 两次削波同步后连接断开");
            return false;
        }

        log("两次削波顺序同步 OK");
        return true;
    }

    private static async Task<bool> WaitForBleWriteIdleAsync(TimeSpan timeout, Action<string> log)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (BleClient.Instance.IsWriteQueueIdle)
                return true;

            if (!HPToyAppService.Instance.IsConnectionReady)
            {
                log("FAIL: 等待 BLE 空闲时断开");
                return false;
            }

            await Task.Delay(100);
        }

        return BleClient.Instance.IsWriteQueueIdle;
    }

    private static async Task<bool> TryAmStoreAsync(Action<string> log)
    {
        log("拍音存硬件测试…");
        var dev = HPToyAppService.Instance.Device;
        var preset = dev.GetActivePreset();
        preset.GetVolume().SetDb(0);
        var volumeBefore = preset.GetVolume().GetDb();

        var am = dev.GetAmMode();
        am.SetEnabled(true);
        am.StoreToPeripheral(null);

        var sawActive = false;
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (BleClient.Instance.State == BleConnectionState.Disconnected && !sawActive)
            {
                log("FAIL: 拍音写入后连接断开");
                return false;
            }

            if (BleClient.Instance.IsFlashTransferActive)
                sawActive = true;
            else if (sawActive)
            {
                await Task.Delay(1500);
                if (HPToyAppService.Instance.IsConnectionReady ||
                    BleClient.Instance.IsConnected)
                {
                    var volumeAfter = dev.GetActivePreset().GetVolume().GetDb();
                    if (Math.Abs(volumeAfter - volumeBefore) > 0.5f)
                    {
                        log($"FAIL: 拍音保存后音量从 {volumeBefore:F1} dB 变为 {volumeAfter:F1} dB");
                        return false;
                    }

                    log("拍音存硬件 OK");
                    return true;
                }
            }

            await Task.Delay(200);
        }

        log("FAIL: 拍音写入超时或未恢复连接");
        return false;
    }

    private static bool ChecksumsMatchedInLog(string bleLog) =>
        Regex.IsMatch(
            bleLog,
            @"已连接。设备校验=0x([0-9A-F]{4}), 应用=0x\1",
            RegexOptions.CultureInvariant);

    private static async Task FlushLog(StringBuilder log, int exitCode)
    {
        log.AppendLine($"退出码: {exitCode}");
        try { await File.WriteAllTextAsync(LogPath, log.ToString()); } catch { }
    }
}

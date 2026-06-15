namespace HPToy.Win.Helpers;

public static class UiText
{
    private static string L(string en, string zh) =>
        UiLanguageService.IsChinese ? zh : en;

    public static string AppTitle { get; private set; } = "";
    public static string Connect { get; private set; } = "";
    public static string Disconnect { get; private set; } = "";
    public static string Ready { get; private set; } = "";
    public static string WriteCompleted { get; private set; } = "";
    public static string StatusDisconnected { get; private set; } = "";
    public static string StatusConnecting { get; private set; } = "";
    public static string StatusConnectedInit { get; private set; } = "";
    public static string StatusReady { get; private set; } = "";

    public static string TabMain { get; private set; } = "";
    public static string TabBass { get; private set; } = "";
    public static string TabTreble { get; private set; } = "";
    public static string TabLoudness { get; private set; } = "";
    public static string TabFilters { get; private set; } = "";
    public static string TabCompressor { get; private set; } = "";
    public static string TabPresets { get; private set; } = "";
    public static string TabOptions { get; private set; } = "";

    public static string MasterVolume { get; private set; } = "";
    public static string FiltersPeq { get; private set; } = "";
    public static string ActivePreset { get; private set; } = "";
    public static string SavePresetToLibrary { get; private set; } = "";

    public static string BassChannels { get; private set; } = "";
    public static string TrebleChannels { get; private set; } = "";

    public static string LoudnessAmount { get; private set; } = "";
    public static string CrossoverFreq { get; private set; } = "";

    public static string PeqBypass { get; private set; } = "";
    public static string Enabled { get; private set; } = "";
    public static string FrequencyHz { get; private set; } = "";
    public static string QFactor { get; private set; } = "";
    public static string GainDb { get; private set; } = "";
    public static string AddLp { get; private set; } = "";
    public static string AddHp { get; private set; } = "";
    public static string RemoveFilter { get; private set; } = "";

    public static string FilterOff { get; private set; } = "";
    public static string FilterHighPass { get; private set; } = "";
    public static string FilterLowPass { get; private set; } = "";
    public static string FilterParametric { get; private set; } = "";
    public static string FilterAllPass { get; private set; } = "";
    public static string FilterBandPass { get; private set; } = "";

    public static string CompressorAmount { get; private set; } = "";
    public static string ActivePoint { get; private set; } = "";
    public static string InputDb { get; private set; } = "";
    public static string OutputDb { get; private set; } = "";
    public static string TimeConstants { get; private set; } = "";
    public static string EnergyMs { get; private set; } = "";
    public static string AttackMs { get; private set; } = "";
    public static string DecayMs { get; private set; } = "";

    public static string Apply { get; private set; } = "";
    public static string Delete { get; private set; } = "";
    public static string Rename { get; private set; } = "";
    public static string Import { get; private set; } = "";
    public static string Export { get; private set; } = "";
    public static string Refresh { get; private set; } = "";

    public static string Language { get; private set; } = "";
    public static string LanguageEnglish { get; private set; } = "";
    public static string LanguageChinese { get; private set; } = "";

    public static string DeviceActionsSection { get; private set; } = "";
    public static string DeviceName { get; private set; } = "";
    public static string Set { get; private set; } = "";
    public static string ChangePairingCode { get; private set; } = "";
    public static string RestoreFactory { get; private set; } = "";
    public static string ResetSettings { get; private set; } = "";
    public static string ResetSettingsConfirm { get; private set; } = "";
    public static string AutoOffThreshold { get; private set; } = "";
    public static string AutoOffHint { get; private set; } = "";
    public static string ClipThreshold { get; private set; } = "";
    public static string ClipHint { get; private set; } = "";
    public static string EnergySync { get; private set; } = "";
    public static string EnergySyncConfirm { get; private set; } = "";
    public static string EnergySynced { get; private set; } = "";
    public static string AdvertiseMode { get; private set; } = "";
    public static string AdvertiseAlways { get; private set; } = "";
    public static string Advertise1Min { get; private set; } = "";
    public static string OutputMode { get; private set; } = "";
    public static string OutputBalanced { get; private set; } = "";
    public static string OutputUnbalanced { get; private set; } = "";
    public static string OutputBoost { get; private set; } = "";
    public static string AmMode { get; private set; } = "";
    public static string AmModeStore { get; private set; } = "";
    public static string AmModeStored { get; private set; } = "";
    public static string FlashTransferBusy { get; private set; } = "";
    public static string AboutLine1 { get; private set; } = "";
    public static string AboutLine2 { get; private set; } = "";

    public static string DiscoverTitle { get; private set; } = "";
    public static string DiscoverHint { get; private set; } = "";
    public static string Scan { get; private set; } = "";
    public static string Close { get; private set; } = "";
    public static string Demo { get; private set; } = "";
    public static string InputTitle { get; private set; } = "";
    public static string Ok { get; private set; } = "";
    public static string Cancel { get; private set; } = "";
    public static string Scanning { get; private set; } = "";
    public static string ReconnectWorkflow { get; private set; } = "";
    public static string NoSavedDevice { get; private set; } = "";

    public static string VolumePrompt { get; private set; } = "";
    public static string BassPrompt { get; private set; } = "";
    public static string TreblePrompt { get; private set; } = "";
    public static string PresetNamePrompt { get; private set; } = "";
    public static string PairingCodePrompt { get; private set; } = "";
    public static string PairingOldPrompt { get; private set; } = "";
    public static string PairingNewPrompt { get; private set; } = "";
    public static string PairingConfirmPrompt { get; private set; } = "";
    public static string PairingMismatch { get; private set; } = "";
    public static string PairingOldWrong { get; private set; } = "";
    public static string PairingChanged { get; private set; } = "";
    public static string NewNamePrompt { get; private set; } = "";

    public static string ErrorTitle { get; private set; } = "";
    public static string WarningTitle { get; private set; } = "";
    public static string ConfirmTitle { get; private set; } = "";
    public static string PresetsTitle { get; private set; } = "";
    public static string HptoyErrorTitle { get; private set; } = "";
    public static string PresetExistsRewrite { get; private set; } = "";
    public static string CannotDeleteOfficial { get; private set; } = "";
    public static string CannotRenameOfficial { get; private set; } = "";
    public static string RestoreFactoryConfirm { get; private set; } = "";
    public static string PresetMismatchTitle { get; private set; } = "";
    public static string OpenPresetFilter { get; private set; } = "";

    public static string BiquadN(int n) => L($"Biquad {n}", $"滤波器 {n}");
    public static string PointN(int n) => L($"Point {n}", $"控制点 {n}");
    public static string MacLabel(string mac) => L($"MAC: {mac}", $"地址: {mac}");
    public static string FoundDevices(int n) => L($"Found {n} device(s).", $"找到 {n} 个设备。");
    public static string AppliedPreset(string name) =>
        L($"Applied preset: {name}", $"已应用预设：{name}");
    public static string DeleteConfirm(string name) => L($"Delete {name}?", $"删除 {name}？");

    public static string PresetMismatchMsg(short deviceCs, short appCs) =>
        UiLanguageService.IsChinese
            ? $"设备预设校验 0x{deviceCs:X4} 与应用 0x{appCs:X4} 不一致。\n\n是 = 从设备导入\n否 = 推送到设备\n取消 = 跳过"
            : $"Device checksum 0x{deviceCs:X4} ≠ app 0x{appCs:X4}.\n\nYes = Import from device\nNo = Push app preset\nCancel = Skip";

    public static string PresetSyncFromDevice(short deviceCs, short appCs) =>
        UiLanguageService.IsChinese
            ? $"预设与设备不一致 (设备 0x{deviceCs:X4} / 应用 0x{appCs:X4})，正在从设备同步…"
            : $"Preset mismatch (device 0x{deviceCs:X4} / app 0x{appCs:X4}), syncing from device…";

    public static void Reload()
    {
        AppTitle = "hptoy";
        Connect = L("Connect", "连接");
        Disconnect = L("Disconnect", "断开");
        Ready = L("Ready", "就绪");
        WriteCompleted = L("Write completed", "写入完成");

        StatusDisconnected = L("Disconnected", "未连接");
        StatusConnecting = L("Connecting…", "正在连接…");
        StatusConnectedInit = L("Connected — initializing…", "已连接 — 初始化中…");
        StatusReady = L("Connected — ready", "已连接 — 就绪");

        TabMain = L("Main", "主界面");
        TabBass = L("Bass", "低音");
        TabTreble = L("Treble", "高音");
        TabLoudness = L("Loudness", "响度");
        TabFilters = L("Filters", "滤波器");
        TabCompressor = L("Compressor", "压缩器");
        TabPresets = L("Presets", "预设");
        TabOptions = L("Options", "设置");

        MasterVolume = L("Master volume", "主音量");
        FiltersPeq = L("Filters (PEQ)", "滤波器 (PEQ)");
        ActivePreset = L("Active preset", "当前预设");
        SavePresetToLibrary = L("Save preset to library…", "保存预设到库…");

        BassChannels = L("Bass (channels 1+2+7)", "低音 (声道 1+2+7)");
        TrebleChannels = L("Treble (channels 1+2+7)", "高音 (声道 1+2+7)");

        LoudnessAmount = L("Loudness amount", "响度强度");
        CrossoverFreq = L("Crossover frequency", "分频频率");

        PeqBypass = L("PEQ bypass", "PEQ 旁路");
        Enabled = L("Enabled", "启用");
        FrequencyHz = L("Frequency (Hz)", "频率 (Hz)");
        QFactor = L("Q factor", "Q 值");
        GainDb = L("Gain (dB)", "增益 (dB)");
        AddLp = L("Add LP", "添加低通");
        AddHp = L("Add HP", "添加高通");
        RemoveFilter = L("Remove filter", "移除滤波器");

        FilterOff = L("Off", "关闭");
        FilterHighPass = L("High-pass", "高通");
        FilterLowPass = L("Low-pass", "低通");
        FilterParametric = L("Parametric", "参量");
        FilterAllPass = L("All-pass", "全通");
        FilterBandPass = L("Band-pass", "带通");

        CompressorAmount = L("Compressor amount (ch 1)", "压缩量 (声道 1)");
        ActivePoint = L("Active point", "当前控制点");
        InputDb = L("Input dB", "输入 dB");
        OutputDb = L("Output dB", "输出 dB");
        TimeConstants = L("Time constants (ch 1-7)", "时间常数 (声道 1-7)");
        EnergyMs = L("Energy (ms)", "能量 (ms)");
        AttackMs = L("Attack (ms)", "启动 (ms)");
        DecayMs = L("Decay (ms)", "释放 (ms)");

        Apply = L("Apply", "应用");
        Delete = L("Delete", "删除");
        Rename = L("Rename", "重命名");
        Import = L("Import…", "导入…");
        Export = L("Export…", "导出…");
        Refresh = L("Refresh", "刷新");

        Language = L("Language", "界面语言");
        LanguageEnglish = L("English", "English");
        LanguageChinese = L("Chinese", "中文");

        DeviceActionsSection = L("Device actions", "设备操作");
        DeviceName = L("Device name", "设备名称");
        Set = L("Set", "设置");
        ChangePairingCode = L("Change pairing code…", "修改配对码…");
        RestoreFactory = L("Restore factory settings", "恢复出厂设置");
        ResetSettings = L("Reset options to defaults", "重置选项为默认值");
        ResetSettingsConfirm = L(
            "Reset pairing code, energy thresholds, Bluetooth advertise, output mode and AM mode to defaults?\n(Presets and volume are not changed.)",
            "将配对码、能量阈值、蓝牙广播、输出模式、AM 模式恢复默认？\n（不会改动预设和音量等 DSP 参数。）");
        AutoOffThreshold = L("Auto-off threshold (low)", "自动关机阈值 (低)");
        AutoOffHint = L(
            "When input level stays below this value, the device treats it as silence (auto-off / energy detect).",
            "输入电平持续低于此值时，设备视为无信号（与自动关机/能量检测相关）。");
        ClipThreshold = L("Clip threshold", "削波阈值");
        ClipHint = L(
            "When input exceeds this level, the clip indicator (header dot) turns on.",
            "输入电平超过此值时，顶部栏削波指示会亮起。");
        EnergySync = L("Sync energy settings to device", "同步能量设置到设备");
        EnergySyncConfirm = L(
            "Push auto-off and clip threshold settings to the device?",
            "将自动关机阈值和削波阈值写入设备？");
        EnergySynced = L("Energy settings synced.", "能量设置已同步。");
        AdvertiseMode = L("Bluetooth advertise mode", "蓝牙广播模式");
        AdvertiseAlways = L("Always on", "始终开启");
        Advertise1Min = L("Off after 1 minute", "1 分钟后关闭");
        OutputMode = L("Output mode", "输出模式");
        OutputBalanced = L("Balanced", "平衡");
        OutputUnbalanced = L("Unbalanced", "非平衡");
        OutputBoost = L("Unbalanced boost", "非平衡增强");
        AmMode = L("44.1 kHz beat-tones elimination (AM mode)", "44.1 kHz 拍音消除 (AM 模式)");
        AmModeStore = L("Store beat-tones setting to hardware", "将拍音设置存到硬件");
        AmModeStored = L("Beat-tones setting stored to hardware.", "拍音设置已存到硬件。");
        FlashTransferBusy = L(
            "Hardware write in progress. Wait for it to finish before saving again.",
            "正在写入硬件，请等当前操作完成后再保存。");
        AboutLine1 = L("HPToy for Windows — E1DA PowerDAC V2/V2.1", "HPToy Windows 版 — E1DA PowerDAC V2/V2.1");
        AboutLine2 = L("USB: audio only. DSP control via Bluetooth.", "USB 仅负责音频，DSP 通过蓝牙控制。");

        DiscoverTitle = L("Discover HPToy devices", "搜索 HPToy 设备");
        DiscoverHint = L("Select a device and click Connect.", "选中设备后点击连接。");
        ReconnectWorkflow = L("Connecting…", "正在连接…");
        NoSavedDevice = L("No saved device. Use Discover to scan.", "没有已保存的设备，请从列表搜索。");
        Scan = L("Scan", "扫描");
        Close = L("Close", "关闭");
        Demo = L("Demo", "演示");
        InputTitle = L("Input", "输入");
        Ok = L("OK", "确定");
        Cancel = L("Cancel", "取消");
        Scanning = L("Scanning…", "正在扫描…");

        VolumePrompt = L("Volume (dB)", "音量 (dB)");
        BassPrompt = L("Bass (dB)", "低音 (dB)");
        TreblePrompt = L("Treble (dB)", "高音 (dB)");
        PresetNamePrompt = L("Preset name", "预设名称");
        PairingCodePrompt = L("Pairing code (integer)", "配对码 (整数)");
        PairingOldPrompt = L("Old pairing code", "旧配对码");
        PairingNewPrompt = L("New pairing code", "新配对码");
        PairingConfirmPrompt = L("Confirm new pairing code", "确认新配对码");
        PairingMismatch = L("New pairing code and confirmation do not match.", "新配对码与确认不一致。");
        PairingOldWrong = L("Old pairing code is incorrect.", "旧配对码不正确。");
        PairingChanged = L("Pairing code changed successfully.", "配对码修改成功。");
        NewNamePrompt = L("New name", "新名称");

        ErrorTitle = L("Error", "错误");
        WarningTitle = L("Warning", "警告");
        ConfirmTitle = L("Confirm", "确认");
        PresetsTitle = L("Presets", "预设");
        HptoyErrorTitle = L("HPToy error", "HPToy 错误");
        PresetExistsRewrite = L("Preset exists. Rewrite?", "预设已存在，是否覆盖？");
        CannotDeleteOfficial = L("Cannot delete official presets.", "无法删除官方预设。");
        CannotRenameOfficial = L("Cannot rename official presets.", "无法重命名官方预设。");
        RestoreFactoryConfirm = L("Restore factory settings on device?", "在设备上恢复出厂设置？");
        PresetMismatchTitle = L("Preset mismatch", "预设不一致");
        OpenPresetFilter = L("HPToy preset (*.tpr)|*.tpr", "HPToy 预设 (*.tpr)|*.tpr");
    }
}

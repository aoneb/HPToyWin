using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HPToy.Core.Ble;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win;

public partial class MainWindow : Window
{
    private bool _languageUpdating;

    public MainWindow()
    {
        InitializeComponent();
        InitLanguageCombo();
        MainView.NavigateTab += NavigateToTab;
        WireAppEvents();
        UiLanguageService.LanguageChanged += () => Dispatcher.Invoke(ApplyLanguage);
        ApplyLanguage();
    }

    private void InitLanguageCombo()
    {
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiText.LanguageEnglish, Tag = AppLanguage.English });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiText.LanguageChinese, Tag = AppLanguage.Chinese });
        SelectLanguageCombo(LanguageCombo, UiLanguageService.Current);
    }

    private static void SelectLanguageCombo(ComboBox combo, AppLanguage lang)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem ci && ci.Tag is AppLanguage l && l == lang)
            {
                combo.SelectedItem = ci;
                break;
            }
        }
    }

    private void WireAppEvents()
    {
        var app = HPToyAppService.Instance;
        // BeginInvoke only — BLE callbacks must not block on the UI thread (deadlock
        // leaves status stuck at "初始化中" because ConnectionReady never finishes).
        app.ConnectionStateChanged += RefreshHeader;
        app.DeviceChanged += RefreshAll;
        app.LogMessage += msg => LogText.Text = msg;
        app.PairingRequired += ShowPairingDialog;
        app.PresetChecksumMismatch += ShowChecksumDialog;
        app.WriteProgress += count =>
        {
            WriteProgressBar.Visibility = Visibility.Visible;
            WriteProgressBar.Value = 100 - Math.Min(count, 100);
        };
        app.WriteCompleted += () =>
        {
            WriteProgressBar.Visibility = Visibility.Collapsed;
            LogText.Text = UiText.WriteCompleted;
        };
        app.FlashTransferRejected += () =>
            MessageBox.Show(this, UiText.FlashTransferBusy, UiText.WarningTitle);
    }

    public void ApplyLanguage()
    {
        Title = UiText.AppTitle;
        LogText.Text = UiText.Ready;

        foreach (var item in MainTabs.Items)
        {
            if (item is TabItem ti)
            {
                ti.Header = TabHeader(ti.Tag as string);
            }
        }

        _languageUpdating = true;
        InitLanguageCombo();
        _languageUpdating = false;

        MainView.ApplyLanguage();
        BassView.ApplyLanguage();
        TrebleView.ApplyLanguage();
        LoudnessView.ApplyLanguage();
        FiltersView.ApplyLanguage();
        CompressorView.ApplyLanguage();
        PresetsView.ApplyLanguage();
        OptionsView.ApplyLanguage();

        RefreshHeader();
    }

    private static string? TabHeader(string? tag) => tag switch
    {
        "Main" => UiText.TabMain,
        "Bass" => UiText.TabBass,
        "Treble" => UiText.TabTreble,
        "Loudness" => UiText.TabLoudness,
        "Filters" => UiText.TabFilters,
        "Compressor" => UiText.TabCompressor,
        "Presets" => UiText.TabPresets,
        "Options" => UiText.TabOptions,
        _ => tag
    };

    private void RefreshHeader()
    {
        var dev = HPToyAppService.Instance.Device;
        DeviceNameText.Text = dev.GetName();
        ClipIndicator.Fill = dev.GetClipFlag()
            ? new SolidColorBrush(Colors.Red)
            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

        StatusText.Text = HPToyAppService.Instance.ConnectionState switch
        {
            BleConnectionState.ConnectionReady => UiText.StatusReady,
            BleConnectionState.Connected => UiText.StatusConnectedInit,
            BleConnectionState.Connecting => UiText.StatusConnecting,
            _ => UiText.StatusDisconnected
        };

        ConnectButton.Content = HPToyAppService.Instance.ConnectionState == BleConnectionState.Disconnected
            ? UiText.Connect
            : UiText.Disconnect;
    }

    private void RefreshAll()
    {
        RefreshHeader();
        MainView.Refresh();
        BassView.Refresh();
        TrebleView.Refresh();
        LoudnessView.Refresh();
        FiltersView.Refresh();
        CompressorView.Refresh();
        PresetsView.RefreshList();
        OptionsView.Refresh();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_languageUpdating) return;
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is AppLanguage lang)
            UiLanguageService.SetLanguage(lang);
    }

    private void NavigateToTab(string tabName)
    {
        foreach (var item in MainTabs.Items)
        {
            if (item is TabItem ti && (ti.Tag as string) == tabName)
            {
                MainTabs.SelectedItem = ti;
                break;
            }
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (HPToyAppService.Instance.ConnectionState != BleConnectionState.Disconnected)
        {
            await HPToyAppService.Instance.DisconnectAsync();
            RefreshHeader();
            return;
        }

        var dlg = new DeviceDiscoveryDialog { Owner = this };
        if (dlg.ShowDialog() == true)
            RefreshAll();
    }

    private void ShowPairingDialog()
    {
        var dev = HPToyAppService.Instance.Device;
        var dlg = new InputDialog(UiText.PairingCodePrompt, dev.GetPairingCode().ToString()) { Owner = this };
        if (dlg.ShowDialog() == true && int.TryParse(dlg.Result, out var code))
            HPToyAppService.Instance.SubmitPairingCode(code);
    }

    private void ShowChecksumDialog(short deviceChecksum, short appChecksum)
    {
        var result = MessageBox.Show(this, UiText.PresetMismatchMsg(deviceChecksum, appChecksum),
            UiText.PresetMismatchTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            HPToyAppService.Instance.ImportPresetFromDevice();
        else if (result == MessageBoxResult.No)
            HPToyAppService.Instance.PushActivePresetToDevice();
    }
}

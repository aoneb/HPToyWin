using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HPToy.Core.Ble;
using HPToy.Core.Device;
using HPToy.Core.Objects;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class OptionsView : UserControl, ILocalizableView
{
    private bool _updating;
    private bool _energySyncInFlight;
    private DispatcherTimer? _energySyncWatchdog;

    public OptionsView()
    {
        _updating = true;
        InitializeComponent();
        _updating = false;
        HPToyAppService.Instance.DeviceChanged += Refresh;
        HPToyAppService.Instance.WriteCompleted += OnDeviceWriteCompleted;
        Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
    }

    public void ApplyLanguage()
    {
        _updating = true;

        LanguageTitleLabel.Text = UiText.Language;
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiText.LanguageEnglish, Tag = AppLanguage.English });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiText.LanguageChinese, Tag = AppLanguage.Chinese });
        foreach (var item in LanguageCombo.Items)
        {
            if (item is ComboBoxItem ci && ci.Tag is AppLanguage l && l == UiLanguageService.Current)
            {
                LanguageCombo.SelectedItem = ci;
                break;
            }
        }

        DeviceNameLabel.Text = UiText.DeviceName;
        DeviceActionsTitle.Text = UiText.DeviceActionsSection;
        SetNameBtn.Content = UiText.Set;
        PairingCodeBtn.Content = UiText.ChangePairingCode;
        RestoreFactoryBtn.Content = UiText.RestoreFactory;
        ResetSettingsBtn.Content = UiText.ResetSettings;
        AutoOffTitleLabel.Text = UiText.AutoOffThreshold;
        AutoOffHintLabel.Text = UiText.AutoOffHint;
        ClipTitleLabel.Text = UiText.ClipThreshold;
        ClipHintLabel.Text = UiText.ClipHint;
        EnergySyncBtn.Content = UiText.EnergySync;
        AdvertiseTitleLabel.Text = UiText.AdvertiseMode;
        OutputModeTitleLabel.Text = UiText.OutputMode;
        AmModeCheck.Content = UiText.AmMode;
        AmModeStoreBtn.Content = UiText.AmModeStore;
        AboutLine1Label.Text = UiText.AboutLine1;
        AboutLine2Label.Text = UiText.AboutLine2;

        var advIdx = AdvertiseCombo.SelectedIndex;
        AdvertiseCombo.Items.Clear();
        AdvertiseCombo.Items.Add(UiText.AdvertiseAlways);
        AdvertiseCombo.Items.Add(UiText.Advertise1Min);
        if (advIdx >= 0) AdvertiseCombo.SelectedIndex = advIdx;

        var outIdx = OutputModeCombo.SelectedIndex;
        OutputModeCombo.Items.Clear();
        OutputModeCombo.Items.Add(UiText.OutputBalanced);
        OutputModeCombo.Items.Add(UiText.OutputUnbalanced);
        OutputModeCombo.Items.Add(UiText.OutputBoost);
        if (outIdx >= 0) OutputModeCombo.SelectedIndex = outIdx;

        _updating = false;
    }

    public void Refresh()
    {
        _updating = true;
        var dev = HPToyAppService.Instance.Device;
        DeviceNameBox.Text = dev.GetName();
        MacText.Text = UiText.MacLabel(dev.GetMac());

        var ec = dev.GetEnergyConfig();
        AutoOffSlider.Value = ec.GetLowThresholdDbPercent() * 100;
        AutoOffLabel.Text = $"{ec.GetLowThresholdDb():F0} dB";
        ClipSlider.Value = ec.GetHighThresholdDbPercent() * 100;
        ClipLabel.Text = $"{ec.GetHighThresholdDb():F0} dB";

        AdvertiseCombo.SelectedIndex = dev.GetAdvertiseMode().GetMode();
        OutputModeCombo.SelectedIndex = dev.GetOutputMode().GetValue();
        AmModeCheck.IsChecked = dev.GetAmMode().IsEnabled();
        if (!_energySyncInFlight)
            EnergySyncBtn.IsEnabled = HPToyAppService.Instance.IsConnectionReady;
        _updating = false;
    }

    private void CompleteEnergySyncUi(bool showSuccessToast)
    {
        _energySyncWatchdog?.Stop();
        _energySyncWatchdog = null;
        if (!_energySyncInFlight)
            return;

        _energySyncInFlight = false;
        EnergySyncBtn.IsEnabled = HPToyAppService.Instance.IsConnectionReady;

        if (showSuccessToast)
            MessageBox.Show(Window.GetWindow(this), UiText.EnergySynced, UiText.ConfirmTitle);
    }

    private void StartEnergySyncWatchdog()
    {
        _energySyncWatchdog?.Stop();
        _energySyncWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _energySyncWatchdog.Tick += (_, _) => CompleteEnergySyncUi(false);
        _energySyncWatchdog.Start();
    }

    private void PersistSettings()
    {
        HiFiToyDeviceManager.Instance.Store();
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is AppLanguage lang)
            UiLanguageService.SetLanguage(lang);
    }

    private void SetName_Click(object sender, RoutedEventArgs e)
    {
        var dev = HPToyAppService.Instance.Device;
        dev.SetName(DeviceNameBox.Text.Trim());
        PersistSettings();
        HPToyAppService.Instance.NotifyDeviceChanged();
    }

    private void PairingCode_Click(object sender, RoutedEventArgs e)
    {
        var dev = HPToyAppService.Instance.Device;
        // Mirrors the original: require the current code plus a confirmed new code
        // before changing it, so a typo can't silently lock you out of the device.
        var dlg = new PairingCodeDialog(dev.GetPairingCode())
        { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;

        dev.SetPairingCode(dlg.NewCode);
        PersistSettings();
        if (BleClient.Instance.IsConnected)
            BleClient.Instance.SendNewPairingCode(dlg.NewCode);
        MessageBox.Show(Window.GetWindow(this), UiText.PairingChanged, UiText.ConfirmTitle);
    }

    private void RestoreFactory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this),
                UiText.RestoreFactoryConfirm, UiText.ConfirmTitle,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        HPToyAppService.Instance.Device.RestoreFactorySettings(new PostAction(() =>
            HPToyAppService.RunOnUi(() =>
            {
                HPToyAppService.Instance.NotifyDeviceChanged();
                Refresh();
            })));
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this),
                UiText.ResetSettingsConfirm, UiText.ConfirmTitle,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        var dev = HPToyAppService.Instance.Device;
        dev.ResetSettingsToDefault();
        if (BleClient.Instance.IsConnectionReady)
            dev.PushSettingsToDevice();
        HPToyAppService.Instance.NotifyDeviceChanged();
        Refresh();
    }

    private void AutoOff_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var ec = HPToyAppService.Instance.Device.GetEnergyConfig();
        ec.SetLowThresholdDbPercent((float)(AutoOffSlider.Value / 100.0));
        AutoOffLabel.Text = $"{ec.GetLowThresholdDb():F0} dB";
        PersistSettings();
        // Like the original: energy/clip thresholds are applied via the "Sync" button,
        // not live on every drag (avoids flooding the device with energy-config writes).
    }

    private void Clip_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var ec = HPToyAppService.Instance.Device.GetEnergyConfig();
        ec.SetHighThresholdDbPercent((float)(ClipSlider.Value / 100.0));
        ClipLabel.Text = $"{ec.GetHighThresholdDb():F0} dB";
        PersistSettings();
    }

    private void OnDeviceWriteCompleted()
    {
        if (!_energySyncInFlight)
            return;
        CompleteEnergySyncUi(HPToyAppService.Instance.IsConnectionReady);
    }

    private void EnergySync_Click(object sender, RoutedEventArgs e)
    {
        if (!BleClient.Instance.IsConnectionReady)
        {
            MessageBox.Show(Window.GetWindow(this), UiText.StatusDisconnected, UiText.WarningTitle);
            return;
        }

        if (HPToyAppService.Instance.IsFlashTransferActive || _energySyncInFlight)
        {
            MessageBox.Show(Window.GetWindow(this), UiText.FlashTransferBusy, UiText.WarningTitle);
            return;
        }

        if (MessageBox.Show(Window.GetWindow(this), UiText.EnergySyncConfirm, UiText.ConfirmTitle,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        _energySyncInFlight = true;
        EnergySyncBtn.IsEnabled = false;
        StartEnergySyncWatchdog();

        HPToyAppService.Instance.Device.StoreEnergyConfigToPeripheral();
    }

    private void Advertise_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || AdvertiseCombo.SelectedIndex < 0) return;
        HPToyAppService.Instance.Device.GetAdvertiseMode()
            .SetModeWithWriteToDsp((byte)AdvertiseCombo.SelectedIndex);
        PersistSettings();
    }

    private void OutputMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || OutputModeCombo.SelectedIndex < 0) return;
        var dev = HPToyAppService.Instance.Device;
        dev.GetOutputMode().SetValue((byte)OutputModeCombo.SelectedIndex);
        PersistSettings();
        if (BleClient.Instance.IsConnectionReady)
            dev.GetOutputMode().SendToDsp();
    }

    private void AmMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        var am = HPToyAppService.Instance.Device.GetAmMode();
        am.SetEnabled(AmModeCheck.IsChecked == true);
        PersistSettings();
        // Toggle sends to live RAM only (immediate preview). Use the Store button
        // to persist the setting to hardware flash so it survives a power cycle.
        if (BleClient.Instance.IsConnectionReady)
            am.SendToPeripheral(true);
    }

    private void AmModeStore_Click(object sender, RoutedEventArgs e)
    {
        if (!BleClient.Instance.IsConnectionReady)
        {
            MessageBox.Show(Window.GetWindow(this), UiText.StatusDisconnected, UiText.WarningTitle);
            return;
        }

        if (HPToyAppService.Instance.IsFlashTransferActive)
        {
            MessageBox.Show(Window.GetWindow(this), UiText.FlashTransferBusy, UiText.WarningTitle);
            return;
        }

        var am = HPToyAppService.Instance.Device.GetAmMode();
        am.StoreToPeripheral(new PostAction(() =>
            HPToyAppService.RunOnUi(() =>
            {
                HPToyAppService.Instance.NotifyDeviceChanged();
                MessageBox.Show(Window.GetWindow(this), UiText.AmModeStored, UiText.ConfirmTitle);
            })));
    }
}

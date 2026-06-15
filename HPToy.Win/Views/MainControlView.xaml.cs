using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HPToy.Core.Device;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class MainControlView : UserControl, ILocalizableView
{
  private bool _updating;

  public event Action<string>? NavigateTab;

  public MainControlView()
  {
    _updating = true;
    InitializeComponent();
    _updating = false;
    HPToyAppService.Instance.DeviceChanged += Refresh;
    Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
  }

  public void ApplyLanguage()
  {
    MasterVolumeLabel.Text = UiText.MasterVolume;
    NavBassBtn.Content = UiText.TabBass;
    NavTrebleBtn.Content = UiText.TabTreble;
    NavLoudnessBtn.Content = UiText.TabLoudness;
    NavFiltersBtn.Content = UiText.FiltersPeq;
    NavCompressorBtn.Content = UiText.TabCompressor;
    NavPresetsBtn.Content = UiText.TabPresets;
    ActivePresetLabel.Text = UiText.ActivePreset;
    SavePresetBtn.Content = UiText.SavePresetToLibrary;
  }

  public void Refresh()
  {
    _updating = true;
    var preset = HPToyAppService.Instance.Device.GetActivePreset();
    var vol = preset.GetVolume();
    VolumeLabel.Text = vol.GetInfo();
    VolumeSlider.Value = vol.GetDbPercent() * 100;
  PresetNameText.Text = preset.GetName();
    _updating = false;
  }

  private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var vol = HPToyAppService.Instance.Device.GetActivePreset().GetVolume();
    vol.SetDbPercent((float)(VolumeSlider.Value / 100.0));
    VolumeLabel.Text = vol.GetInfo();
    vol.SendToPeripheral(false);
  }

  // Re-send the settled value with response so the device is guaranteed to match
  // the displayed value (drag uses write-without-response, which can be dropped).
  private void VolumeSlider_Commit(object sender, RoutedEventArgs e)
  {
    if (_updating) return;
    var vol = HPToyAppService.Instance.Device.GetActivePreset().GetVolume();
    vol.SetDbPercent((float)(VolumeSlider.Value / 100.0));
    VolumeLabel.Text = vol.GetInfo();
    vol.SendToPeripheral(true);
  }

  private void VolumeLabel_Click(object sender, MouseButtonEventArgs e)
  {
    var vol = HPToyAppService.Instance.Device.GetActivePreset().GetVolume();
    var dlg = new InputDialog(UiText.VolumePrompt, vol.GetDb().ToString("F1")) { Owner = Window.GetWindow(this) };
    if (dlg.ShowDialog() != true) return;
    if (!float.TryParse(dlg.Result, out var db)) return;
    vol.SetDb(db);
    vol.SendToPeripheral(true);
    Refresh();
  }

  private void SavePreset_Click(object sender, RoutedEventArgs e)
  {
    var preset = HPToyAppService.Instance.Device.GetActivePreset();
    var dlg = new InputDialog(UiText.PresetNamePrompt, preset.GetName()) { Owner = Window.GetWindow(this) };
    if (dlg.ShowDialog() != true) return;
    var name = dlg.Result.Trim();
    if (string.IsNullOrEmpty(name)) return;

    try
    {
      var clone = (ToyPreset)preset.Clone();
      clone.SetName(name);
      clone.UpdateChecksum();
      clone.Save(false);
      HPToyAppService.Instance.Device.SetActiveKeyPreset(name);
      clone.StoreToPeripheral();
      HPToyAppService.Instance.NotifyDeviceChanged();
      Refresh();
    }
    catch (IOException ex) when (ex.Message == "Preset with this name already exist!")
    {
      if (MessageBox.Show(Window.GetWindow(this), UiText.PresetExistsRewrite, UiText.WarningTitle,
              MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
      try
      {
        var clone = (ToyPreset)preset.Clone();
        clone.SetName(name);
        clone.UpdateChecksum();
        clone.Save(true);
        HPToyAppService.Instance.Device.SetActiveKeyPreset(name);
        clone.StoreToPeripheral();
        HPToyAppService.Instance.NotifyDeviceChanged();
        Refresh();
      }
      catch (Exception ex2)
      {
        MessageBox.Show(Window.GetWindow(this), ex2.Message, UiText.ErrorTitle);
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show(Window.GetWindow(this), ex.Message, UiText.ErrorTitle);
    }
  }

  private void NavBass_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Bass");
  private void NavTreble_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Treble");
  private void NavLoudness_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Loudness");
  private void NavFilters_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Filters");
  private void NavCompressor_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Compressor");
  private void NavPresets_Click(object sender, RoutedEventArgs e) => NavigateTab?.Invoke("Presets");
}

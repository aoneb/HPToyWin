using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class BassView : UserControl, ILocalizableView
{
  private bool _updating;

  public BassView()
  {
    _updating = true;
    InitializeComponent();
    _updating = false;
    HPToyAppService.Instance.DeviceChanged += Refresh;
    Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
  }

  public void ApplyLanguage() => TitleLabel.Text = UiText.BassChannels;

  public void Refresh()
  {
    _updating = true;
    var ch = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().GetBassTreble127();
    BassLabel.Text = $"{DspUiHelper.BassTrebleDbToInt(ch.GetBassDb())} dB";
    BassSlider.Value = ch.GetBassDbPercent() * 100;
    _updating = false;
  }

  private void BassSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var bt = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble();
    var ch = bt.GetBassTreble127();
    ch.SetBassDbPercent((float)(BassSlider.Value / 100.0));
    BassLabel.Text = $"{DspUiHelper.BassTrebleDbToInt(ch.GetBassDb())} dB";
    bt.SendToPeripheral(false);
  }

  private void BassLabel_Click(object sender, MouseButtonEventArgs e)
  {
    var ch = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().GetBassTreble127();
    var dlg = new InputDialog(UiText.BassPrompt, DspUiHelper.BassTrebleDbToInt(ch.GetBassDb()).ToString())
    { Owner = Window.GetWindow(this) };
    if (dlg.ShowDialog() != true || !int.TryParse(dlg.Result, out var db)) return;
    DspUiHelper.SetBassDb(ch, db);
    HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().SendToPeripheral(true);
    Refresh();
  }
}

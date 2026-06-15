using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class TrebleView : UserControl, ILocalizableView
{
  private bool _updating;

  public TrebleView()
  {
    _updating = true;
    InitializeComponent();
    _updating = false;
    HPToyAppService.Instance.DeviceChanged += Refresh;
    Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
  }

  public void ApplyLanguage() => TitleLabel.Text = UiText.TrebleChannels;

  public void Refresh()
  {
    _updating = true;
    var ch = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().GetBassTreble127();
    TrebleLabel.Text = $"{DspUiHelper.BassTrebleDbToInt(ch.GetTrebleDb())} dB";
    TrebleSlider.Value = ch.GetTrebleDbPercent() * 100;
    _updating = false;
  }

  private void TrebleSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var bt = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble();
    var ch = bt.GetBassTreble127();
    ch.SetTrebleDbPercent((float)(TrebleSlider.Value / 100.0));
    TrebleLabel.Text = $"{DspUiHelper.BassTrebleDbToInt(ch.GetTrebleDb())} dB";
    bt.SendToPeripheral(false);
  }

  private void TrebleLabel_Click(object sender, MouseButtonEventArgs e)
  {
    var ch = HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().GetBassTreble127();
    var dlg = new InputDialog(UiText.TreblePrompt, DspUiHelper.BassTrebleDbToInt(ch.GetTrebleDb()).ToString())
    { Owner = Window.GetWindow(this) };
    if (dlg.ShowDialog() != true || !int.TryParse(dlg.Result, out var db)) return;
    DspUiHelper.SetTrebleDb(ch, db);
    HPToyAppService.Instance.Device.GetActivePreset().GetBassTreble().SendToPeripheral(true);
    Refresh();
  }
}

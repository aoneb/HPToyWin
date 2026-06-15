using System.Windows;
using System.Windows.Controls;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class LoudnessView : UserControl, ILocalizableView
{
  private bool _updating;

  public LoudnessView()
  {
    _updating = true;
    InitializeComponent();
    _updating = false;
    HPToyAppService.Instance.DeviceChanged += Refresh;
    Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
  }

  public void ApplyLanguage()
  {
    GainTitleLabel.Text = UiText.LoudnessAmount;
    FreqTitleLabel.Text = UiText.CrossoverFreq;
  }

  public void Refresh()
  {
    _updating = true;
    var l = HPToyAppService.Instance.Device.GetActivePreset().GetLoudness();
    GainLabel.Text = l.GetInfo();
    GainSlider.Value = l.GetGain() * 100;
    FreqLabel.Text = l.GetFreqInfo();
    FreqSlider.Value = l.GetFreq();
    _updating = false;
  }

  private void GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var l = HPToyAppService.Instance.Device.GetActivePreset().GetLoudness();
    l.SetGain((float)(GainSlider.Value / 100.0));
    GainLabel.Text = l.GetInfo();
    l.SendToPeripheral(false);
  }

  private void FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var l = HPToyAppService.Instance.Device.GetActivePreset().GetLoudness();
    l.SetFreq((short)FreqSlider.Value);
    FreqLabel.Text = l.GetFreqInfo();
    l.SendFreqToPeripheral(false);
  }
}

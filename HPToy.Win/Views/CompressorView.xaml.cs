using System.Windows;
using System.Windows.Controls;
using HPToy.Core.Objects;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Views;

public partial class CompressorView : UserControl, ILocalizableView
{
  private bool _updating;

  public CompressorView()
  {
    _updating = true;
    InitializeComponent();
    _updating = false;
    HPToyAppService.Instance.DeviceChanged += Refresh;
    Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
  }

  public void ApplyLanguage()
  {
    AmountTitleLabel.Text = UiText.CompressorAmount;
    ActivePointLabel.Text = UiText.ActivePoint;
    InputTitleLabel.Text = UiText.InputDb;
    OutputTitleLabel.Text = UiText.OutputDb;
    TimeConstTitleLabel.Text = UiText.TimeConstants;
    EnergyTitleLabel.Text = UiText.EnergyMs;
    AttackTitleLabel.Text = UiText.AttackMs;
    DecayTitleLabel.Text = UiText.DecayMs;

    var pointIdx = PointCombo.SelectedIndex;
    PointCombo.Items.Clear();
    for (var i = 0; i < 4; i++)
      PointCombo.Items.Add(UiText.PointN(i));
    if (pointIdx >= 0 && pointIdx < 4) PointCombo.SelectedIndex = pointIdx;
    else PointCombo.SelectedIndex = 2;
  }

  public void Refresh()
  {
    _updating = true;
    var drc = HPToyAppService.Instance.Device.GetActivePreset().GetDrc();
    AmountLabel.Text = $"{(int)(drc.GetEnabledChannel(0) * 100)}%";
    AmountSlider.Value = drc.GetEnabledChannel(0) * 100;
    LoadPoint();
    LoadTimeConst(drc.GetTimeConst17());
    _updating = false;
  }

  private void LoadPoint()
  {
    var coef = HPToyAppService.Instance.Device.GetActivePreset().GetDrc().GetCoef17();
    var idx = PointCombo.SelectedIndex;
    if (idx < 0) idx = 2;
    var pt = coef.GetPoints()[idx];
    InputSlider.Value = pt.InputDb;
    OutputSlider.Value = pt.OutputDb;
    InputLabel.Text = $"{pt.InputDb:F1}";
    OutputLabel.Text = $"{pt.OutputDb:F1}";
  }

  private void LoadTimeConst(DrcTimeConst tc)
  {
    EnergySlider.Value = tc.GetEnergyMS();
    AttackSlider.Value = tc.GetAttackMS();
    DecaySlider.Value = tc.GetDecayMS();
    EnergyLabel.Text = $"{tc.GetEnergyMS():F2}";
    AttackLabel.Text = $"{tc.GetAttackMS():F1}";
    DecayLabel.Text = $"{tc.GetDecayMS():F0}";
  }

  private void AmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var drc = HPToyAppService.Instance.Device.GetActivePreset().GetDrc();
    drc.SetEnabled((float)(AmountSlider.Value / 100.0), 0);
    AmountLabel.Text = $"{(int)AmountSlider.Value}%";
    drc.SendEnabledToPeripheral(0, false);
  }

  private void PointCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (_updating) return;
    LoadPoint();
  }

  private void PointSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating) return;
    var coef = HPToyAppService.Instance.Device.GetActivePreset().GetDrc().GetCoef17();
    var idx = PointCombo.SelectedIndex;
    if (idx < 0) return;
    var pt = coef.GetPoints()[idx];
    pt.InputDb = (float)InputSlider.Value;
    pt.OutputDb = (float)OutputSlider.Value;
    InputLabel.Text = $"{pt.InputDb:F1}";
    OutputLabel.Text = $"{pt.OutputDb:F1}";
    coef.SendToPeripheral(false);
  }

  private void TimeConst_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
  {
    if (_updating || EnergySlider == null || AttackSlider == null || DecaySlider == null) return;
    var tc = HPToyAppService.Instance.Device.GetActivePreset().GetDrc().GetTimeConst17();
    tc.SetEnergyMS((float)EnergySlider.Value);
    tc.SetAttackMS((float)AttackSlider.Value);
    tc.SetDecayMS((float)DecaySlider.Value);
    EnergyLabel.Text = $"{tc.GetEnergyMS():F2}";
    AttackLabel.Text = $"{tc.GetAttackMS():F1}";
    DecayLabel.Text = $"{tc.GetDecayMS():F0}";
    tc.SendToPeripheral(false);
  }
}

using System.Windows;
using System.Windows.Controls;
using HPToy.Core.Objects;
using HPToy.Win.Helpers;
using HPToy.Win.Services;
using static HPToy.Core.Objects.Biquad.BiquadParam.Type;

namespace HPToy.Win.Views;

public partial class FiltersView : UserControl, ILocalizableView
{
    private bool _updating;

    private static (string Label, byte Value)[] GetFilterTypes() => new[]
    {
        (UiText.FilterOff, BIQUAD_OFF),
        (UiText.FilterHighPass, BIQUAD_HIGHPASS),
        (UiText.FilterLowPass, BIQUAD_LOWPASS),
        (UiText.FilterParametric, BIQUAD_PARAMETRIC),
        (UiText.FilterAllPass, BIQUAD_ALLPASS),
        (UiText.FilterBandPass, BIQUAD_BANDPASS)
    };

    public FiltersView()
    {
        _updating = true;
        InitializeComponent();
        _updating = false;
        HPToyAppService.Instance.DeviceChanged += Refresh;
        Loaded += (_, _) => { ApplyLanguage(); Refresh(); };
    }

    public void ApplyLanguage()
    {
        _updating = true;
        PeqBypassCheck.Content = UiText.PeqBypass;
        EnabledCheck.Content = UiText.Enabled;
        FreqTitleLabel.Text = UiText.FrequencyHz;
        QTitleLabel.Text = UiText.QFactor;
        DbTitleLabel.Text = UiText.GainDb;
        AddLpBtn.Content = UiText.AddLp;
        AddHpBtn.Content = UiText.AddHp;
        RemoveFilterBtn.Content = UiText.RemoveFilter;

        var biquadIdx = BiquadCombo.SelectedIndex;
        BiquadCombo.Items.Clear();
        for (var i = 0; i < 7; i++)
            BiquadCombo.Items.Add(UiText.BiquadN(i + 1));
        if (biquadIdx >= 0 && biquadIdx < 7) BiquadCombo.SelectedIndex = biquadIdx;

        var typeIdx = TypeCombo.SelectedIndex;
        TypeCombo.Items.Clear();
        foreach (var t in GetFilterTypes())
            TypeCombo.Items.Add(t.Label);
        if (typeIdx >= 0 && typeIdx < TypeCombo.Items.Count) TypeCombo.SelectedIndex = typeIdx;

        _updating = false;
    }

    public void Refresh()
    {
        _updating = true;
        if (BiquadCombo.SelectedIndex < 0) BiquadCombo.SelectedIndex = 0;
        var filters = HPToyAppService.Instance.Device.GetActivePreset().Filters;
        PeqBypassCheck.IsChecked = !filters.IsPEQEnabled();
        LoadBiquad(filters);
        _updating = false;
    }

    private void LoadBiquad(Filters filters)
    {
        var idx = (byte)BiquadCombo.SelectedIndex;
        filters.SetActiveBiquadIndex(idx);
        var b = filters.GetActiveBiquad();
        if (b == null) return;
        var p = b.GetParams();
        var types = GetFilterTypes();
        EnabledCheck.IsChecked = b.IsEnabled();
        TypeCombo.SelectedIndex = Array.FindIndex(types, t => t.Value == p.GetTypeValue());
        FreqSlider.Value = p.GetFreq();
        FreqLabel.Text = $"{p.GetFreq()} Hz";
        QSlider.Value = p.GetQFac();
        QLabel.Text = $"{p.GetQFac():F2}";
        DbSlider.Value = p.GetDbVolume();
        DbLabel.Text = $"{p.GetDbVolume():F1} dB";
    }

    private Biquad? ActiveBiquad() =>
        HPToyAppService.Instance.Device.GetActivePreset().Filters.GetActiveBiquad();

    private void BiquadCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        LoadBiquad(HPToyAppService.Instance.Device.GetActivePreset().Filters);
    }

    private void PeqBypass_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        var filters = HPToyAppService.Instance.Device.GetActivePreset().Filters;
        filters.SetPEQEnabled(PeqBypassCheck.IsChecked != true);
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        var b = ActiveBiquad();
        if (b == null) return;
        b.SetEnabled(EnabledCheck.IsChecked == true);
        b.SendToPeripheral(true);
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || TypeCombo.SelectedIndex < 0) return;
        var b = ActiveBiquad();
        if (b == null) return;
        b.GetParams().SetTypeValue(GetFilterTypes()[TypeCombo.SelectedIndex].Value);
        b.SendToPeripheral(true);
        LoadBiquad(HPToyAppService.Instance.Device.GetActivePreset().Filters);
    }

    private void FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var b = ActiveBiquad();
        if (b == null) return;
        b.GetParams().SetFreq((short)FreqSlider.Value);
        FreqLabel.Text = $"{b.GetParams().GetFreq()} Hz";
        b.SendToPeripheral(false);
    }

    private void QSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var b = ActiveBiquad();
        if (b == null) return;
        b.GetParams().SetQFac((float)QSlider.Value);
        QLabel.Text = $"{b.GetParams().GetQFac():F2}";
        b.SendToPeripheral(false);
    }

    private void DbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        var b = ActiveBiquad();
        if (b == null) return;
        b.GetParams().SetDbVolume((float)DbSlider.Value);
        DbLabel.Text = $"{b.GetParams().GetDbVolume():F1} dB";
        b.SendToPeripheral(false);
    }

    private void AddLp_Click(object sender, RoutedEventArgs e)
    {
        HPToyAppService.Instance.Device.GetActivePreset().Filters.UpOrderFor(BIQUAD_LOWPASS);
        Refresh();
    }

    private void AddHp_Click(object sender, RoutedEventArgs e)
    {
        HPToyAppService.Instance.Device.GetActivePreset().Filters.UpOrderFor(BIQUAD_HIGHPASS);
        Refresh();
    }

    private void RemoveFilter_Click(object sender, RoutedEventArgs e)
    {
        var b = ActiveBiquad();
        if (b == null) return;
        var type = b.GetParams().GetTypeValue();
        if (type == BIQUAD_LOWPASS || type == BIQUAD_HIGHPASS)
            HPToyAppService.Instance.Device.GetActivePreset().Filters.DownOrderFor(type);
        Refresh();
    }
}

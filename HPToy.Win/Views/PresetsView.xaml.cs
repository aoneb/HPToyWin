using System.IO;
using System.Windows;
using System.Windows.Controls;
using HPToy.Core.Device;
using HPToy.Win.Dialogs;
using HPToy.Win.Helpers;
using HPToy.Win.Services;
using Microsoft.Win32;

namespace HPToy.Win.Views;

public partial class PresetsView : UserControl, ILocalizableView
{
  public PresetsView()
  {
    InitializeComponent();
    HPToyAppService.Instance.DeviceChanged += () => Dispatcher.BeginInvoke(RefreshList);
    Loaded += (_, _) => { ApplyLanguage(); RefreshList(); };
  }

  public void ApplyLanguage()
  {
    ApplyBtn.Content = UiText.Apply;
    DeleteBtn.Content = UiText.Delete;
    RenameBtn.Content = UiText.Rename;
    ImportBtn.Content = UiText.Import;
    ExportBtn.Content = UiText.Export;
    RefreshBtn.Content = UiText.Refresh;
  }

  public void RefreshList()
  {
    var names = HiFiToyPresetManager.Instance.GetPresetNameList();
    var items = names.Select(n => new PresetItem(n)).ToList();
    var active = HPToyAppService.Instance.Device.GetActiveKeyPreset();
    PresetList.ItemsSource = items;
    PresetList.SelectedItem = items.FirstOrDefault(i => i.Name == active);
  }

  private void PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

  private void RefreshList_Click(object sender, RoutedEventArgs e) => RefreshList();

  private void Apply_Click(object sender, RoutedEventArgs e)
  {
    if (PresetList.SelectedItem is not PresetItem item) return;
    HPToyAppService.Instance.ApplyPreset(item.Name);
    MessageBox.Show(Window.GetWindow(this), UiText.AppliedPreset(item.Name), UiText.PresetsTitle);
  }

  private void Delete_Click(object sender, RoutedEventArgs e)
  {
    if (PresetList.SelectedItem is not PresetItem item) return;
    if (HiFiToyPresetManager.Instance.IsOfficialPresetExist(item.Name))
    {
      MessageBox.Show(Window.GetWindow(this), UiText.CannotDeleteOfficial, UiText.PresetsTitle);
      return;
    }
    if (MessageBox.Show(Window.GetWindow(this), UiText.DeleteConfirm(item.Name), UiText.ConfirmTitle,
            MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
    HiFiToyPresetManager.Instance.DeletePreset(item.Name);
    RefreshList();
  }

  private void Rename_Click(object sender, RoutedEventArgs e)
  {
    if (PresetList.SelectedItem is not PresetItem item) return;
    if (HiFiToyPresetManager.Instance.IsOfficialPresetExist(item.Name))
    {
      MessageBox.Show(Window.GetWindow(this), UiText.CannotRenameOfficial, UiText.PresetsTitle);
      return;
    }
    var dlg = new InputDialog(UiText.NewNamePrompt, item.Name) { Owner = Window.GetWindow(this) };
    if (dlg.ShowDialog() != true) return;
    try
    {
      HiFiToyPresetManager.Instance.RenamePreset(item.Name, dlg.Result.Trim());
      if (HPToyAppService.Instance.Device.GetActiveKeyPreset() == item.Name)
        HPToyAppService.Instance.Device.ForceSetActiveKeyPreset(dlg.Result.Trim());
      RefreshList();
    }
    catch (Exception ex)
    {
      MessageBox.Show(Window.GetWindow(this), ex.Message, UiText.ErrorTitle);
    }
  }

  private void Import_Click(object sender, RoutedEventArgs e)
  {
    var dlg = new OpenFileDialog { Filter = UiText.OpenPresetFilter };
    if (dlg.ShowDialog() != true) return;
    try
    {
      HiFiToyPresetManager.Instance.ImportPreset(dlg.FileName);
      RefreshList();
    }
    catch (Exception ex)
    {
      MessageBox.Show(Window.GetWindow(this), ex.Message, UiText.ErrorTitle);
    }
  }

  private void Export_Click(object sender, RoutedEventArgs e)
  {
    if (PresetList.SelectedItem is not PresetItem item) return;
    var dlg = new SaveFileDialog
    {
      Filter = UiText.OpenPresetFilter,
      FileName = item.Name + ".tpr"
    };
    if (dlg.ShowDialog() != true) return;
    try
    {
      var preset = HiFiToyPresetManager.Instance.GetPreset(item.Name);
      File.WriteAllText(dlg.FileName, preset.ToXmlData().ToString());
    }
    catch (Exception ex)
    {
      MessageBox.Show(Window.GetWindow(this), ex.Message, UiText.ErrorTitle);
    }
  }

  private sealed record PresetItem(string Name);
}

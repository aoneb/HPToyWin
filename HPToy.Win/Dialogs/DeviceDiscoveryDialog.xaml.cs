using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HPToy.Core.Ble;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win.Dialogs;

public partial class DeviceDiscoveryDialog : Window
{
    private readonly BleScanner _scanner = new();
    private readonly ObservableCollection<DeviceListItem> _items = new();

    public DeviceDiscoveryDialog()
    {
        InitializeComponent();
        ApplyLanguage();
        DeviceList.ItemsSource = _items;
        _scanner.DeviceFound += OnDeviceFound;
        _scanner.ScanStatus += OnScanStatus;
        Loaded += (_, _) => StartScan();
    }

    private void ApplyLanguage()
    {
        Title = UiText.DiscoverTitle;
        HintText.Text = UiText.DiscoverHint;
        ScanBtn.Content = UiText.Scan;
        ConnectBtn.Content = UiText.Connect;
        CloseBtn.Content = UiText.Close;
    }

    private void OnDeviceFound(BleDeviceInfo info)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_items.Any(i => i.Info.DisplayAddress == info.DisplayAddress))
                return;

            _items.Add(new DeviceListItem(info));
            StatusText.Text = UiText.FoundDevices(_items.Count);
        });
    }

    private void OnScanStatus(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_items.Count == 0)
                StatusText.Text = message;
        });
    }

    private void StartScan()
    {
        _items.Clear();
        StatusText.Text = UiText.Scanning;
        _scanner.Start();
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => StartScan();

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceList.SelectedItem is not DeviceListItem item)
        {
            StatusText.Text = "请先选中列表里的设备";
            return;
        }

        ConnectBtn.IsEnabled = false;
        ScanBtn.IsEnabled = false;
        var logPath = BleClient.LogFilePath;
        try { File.WriteAllText(logPath, ""); } catch { }
        SetStatus($"连接中… 日志: {logPath}");

        _scanner.Stop();
        await Task.Delay(500);

        void OnLog(string msg) => SetStatus(msg);
        BleClient.Instance.Log += OnLog;

        try
        {
            await HPToyAppService.Instance.ConnectAsync(item.Info);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            ConnectBtn.IsEnabled = true;
            ScanBtn.IsEnabled = true;
            StartScan();
        }
        finally
        {
            BleClient.Instance.Log -= OnLog;
        }
    }

    private void SetStatus(string text)
    {
        if (Dispatcher.CheckAccess())
        {
            StatusText.Text = text;
            Dispatcher.Invoke(DispatcherPriority.Render, (Action)(() => { }));
        }
        else
            Dispatcher.Invoke(() => SetStatus(text));
    }

    protected override void OnClosed(EventArgs e)
    {
        _scanner.Stop();
        _scanner.Dispose();
        base.OnClosed(e);
    }

    private sealed class DeviceListItem
    {
        public BleDeviceInfo Info { get; }
        public string DisplayName => $"{Info.Name}  ({Info.DisplayAddress})";

        public DeviceListItem(BleDeviceInfo info) => Info = info;
    }
}

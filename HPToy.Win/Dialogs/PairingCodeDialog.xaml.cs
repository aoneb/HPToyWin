using System.Windows;
using HPToy.Win.Helpers;

namespace HPToy.Win.Dialogs;

public partial class PairingCodeDialog : Window
{
    private readonly int _currentCode;

    public int NewCode { get; private set; }

    public PairingCodeDialog(int currentCode)
    {
        InitializeComponent();
        _currentCode = currentCode;
        Title = UiText.ChangePairingCode;
        OldLabel.Text = UiText.PairingOldPrompt;
        NewLabel.Text = UiText.PairingNewPrompt;
        ConfirmLabel.Text = UiText.PairingConfirmPrompt;
        OkBtn.Content = UiText.Ok;
        CancelBtn.Content = UiText.Cancel;
        OldBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(OldBox.Text, out var oldCode) ||
            !int.TryParse(NewBox.Text, out var newCode) ||
            !int.TryParse(ConfirmBox.Text, out var confirmCode))
        {
            MessageBox.Show(this, UiText.PairingMismatch, UiText.WarningTitle);
            return;
        }

        if (newCode != confirmCode)
        {
            MessageBox.Show(this, UiText.PairingMismatch, UiText.WarningTitle);
            return;
        }

        if (oldCode != _currentCode)
        {
            MessageBox.Show(this, UiText.PairingOldWrong, UiText.WarningTitle);
            return;
        }

        NewCode = newCode;
        DialogResult = true;
    }
}

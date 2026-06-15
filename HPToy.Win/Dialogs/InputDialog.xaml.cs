using System.Windows;
using HPToy.Win.Helpers;

namespace HPToy.Win.Dialogs;

public partial class InputDialog : Window
{
    public string Result { get; private set; } = "";

    public InputDialog(string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = UiText.InputTitle;
        OkBtn.Content = UiText.Ok;
        CancelBtn.Content = UiText.Cancel;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }
}

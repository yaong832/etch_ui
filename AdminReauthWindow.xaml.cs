using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class AdminReauthWindow : Window
{
    private readonly DatabaseService _databaseService;

    public AdminReauthWindow(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtMsg.Text = string.Empty;
        if (SessionContext.CurrentUser is not { } user)
        {
            TxtMsg.Text = "로그인 세션이 없습니다.";
            return;
        }

        if (!_databaseService.TryVerifyUserPassword(user.Id, TxtPassword.Password))
        {
            TxtMsg.Text = "비밀번호가 올바르지 않습니다.";
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

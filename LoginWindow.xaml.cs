using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class LoginWindow : Window
{
    private readonly DatabaseService _databaseService;

    public LoginWindow(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        ResetFields();
    }

    /// <summary>로그아웃 후 재표시 시 입력·오류 메시지 초기화.</summary>
    public void ResetFields()
    {
        TxtUsername.Clear();
        TxtPassword.Clear();
        TxtError.Text = string.Empty;
        TxtUsername.Focus();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtUsername.Clear();
        TxtPassword.Clear();
        TxtError.Text = string.Empty;
        TxtUsername.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = string.Empty;

        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            TxtError.Text = "아이디를 입력하세요.";
            return;
        }

        if (!PasswordPolicy.IsValid(password, out string policyMessage))
        {
            TxtError.Text = policyMessage;
            return;
        }

        UserAccount? account = _databaseService.Authenticate(username, password);
        if (account is null)
        {
            TxtError.Text = "로그인 실패: 아이디 또는 비밀번호를 확인하세요.";
            return;
        }

        SessionContext.SetUser(account);
        DialogResult = true;
        Close();
    }
}

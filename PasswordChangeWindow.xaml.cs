using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class PasswordChangeWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly int? _adminTargetUserId;
    private readonly string? _adminTargetUsername;

    /// <summary>본인 비밀번호 변경.</summary>
    public PasswordChangeWindow(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        InitializeComponent();
    }

    /// <summary>관리자 — 대상 사용자 비밀번호 재설정.</summary>
    public PasswordChangeWindow(DatabaseService databaseService, int targetUserId, string targetUsername)
    {
        _databaseService = databaseService;
        _adminTargetUserId = targetUserId;
        _adminTargetUsername = targetUsername;
        InitializeComponent();
        TxtTitle.Text = "비밀번호 재설정";
        TxtSubtitle.Text = $"관리자가 「{targetUsername}」 계정의 비밀번호를 설정합니다.";
        PanelCurrent.Visibility = Visibility.Collapsed;
        Title = $"비밀번호 재설정 — {targetUsername}";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtMsg.Text = string.Empty;
        TxtMsg.Foreground = System.Windows.Media.Brushes.DarkRed;

        string newPass = TxtNew.Password;
        string confirm = TxtConfirm.Password;
        if (newPass != confirm)
        {
            TxtMsg.Text = "새 비밀번호와 확인이 일치하지 않습니다.";
            return;
        }

        if (_adminTargetUserId is int targetId)
        {
            if (!_databaseService.TrySetPasswordByAdmin(targetId, newPass, out string err))
            {
                TxtMsg.Text = err;
                return;
            }

            string actor = SessionContext.CurrentUser?.Username ?? "?";
            _databaseService.AppendEventLog(actor, null, null, $"비밀번호 재설정: {_adminTargetUsername}");
            DialogResult = true;
            Close();
            return;
        }

        if (SessionContext.CurrentUser is null)
        {
            TxtMsg.Text = "로그인 세션이 없습니다.";
            return;
        }

        if (!_databaseService.TryChangeOwnPassword(
                SessionContext.CurrentUser.Id,
                TxtCurrent.Password,
                newPass,
                out string ownErr))
        {
            TxtMsg.Text = ownErr;
            return;
        }

        _databaseService.AppendEventLog(SessionContext.CurrentUser.Username, null, null, "비밀번호 변경");
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

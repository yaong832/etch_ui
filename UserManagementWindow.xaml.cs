using System.Collections.ObjectModel;
using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class UserManagementWindow : Window
{
    private readonly DatabaseService _databaseService;
    private readonly ObservableCollection<UserListRow> _rows = new();

    public UserManagementWindow(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        GrdUsers.ItemsSource = _rows;

        CmbNewRole.ItemsSource = new[]
        {
            new RolePick("작업자", UserRole.Worker),
            new RolePick("관리자", UserRole.Admin)
        };
        CmbNewRole.DisplayMemberPath = nameof(RolePick.Label);
        CmbNewRole.SelectedValuePath = nameof(RolePick.Role);
        CmbNewRole.SelectedIndex = 0;

        Loaded += (_, _) =>
        {
            if (!SessionContext.HasRole(UserRole.Admin))
            {
                MessageBox.Show(this, "관리자만 사용자 관리를 열 수 있습니다.", "권한 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            RefreshGrid();
        };
    }

    private sealed record RolePick(string Label, UserRole Role);

    private void RefreshGrid()
    {
        _rows.Clear();
        foreach (UserListRow row in _databaseService.GetAllUsers())
        {
            _rows.Add(row);
        }
    }

    private void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        TxtRegisterMsg.Text = string.Empty;
        TxtRegisterMsg.Foreground = System.Windows.Media.Brushes.DarkRed;

        string username = TxtNewUsername.Text.Trim();
        string pass = TxtNewPassword.Password;
        string pass2 = TxtNewPasswordConfirm.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            TxtRegisterMsg.Text = "아이디를 입력하세요.";
            return;
        }

        if (pass != pass2)
        {
            TxtRegisterMsg.Text = "비밀번호와 확인이 일치하지 않습니다.";
            return;
        }

        if (CmbNewRole.SelectedValue is not UserRole role)
        {
            TxtRegisterMsg.Text = "역할을 선택하세요.";
            return;
        }

        if (!_databaseService.TryAddUser(username, pass, role, out string err))
        {
            TxtRegisterMsg.Text = err;
            return;
        }

        string actor = SessionContext.CurrentUser?.Username ?? "?";
        _databaseService.AppendEventLog(actor, null, null, $"사용자 등록: {username} ({role.ToDisplayKorean()})");

        TxtNewUsername.Clear();
        TxtNewPassword.Clear();
        TxtNewPasswordConfirm.Clear();
        CmbNewRole.SelectedIndex = 0;
        TxtRegisterMsg.Foreground = System.Windows.Media.Brushes.ForestGreen;
        TxtRegisterMsg.Text = $"등록 완료: {username}";
        RefreshGrid();
    }

    private void BtnToggleActive_Click(object sender, RoutedEventArgs e)
    {
        TxtRegisterMsg.Text = string.Empty;
        TxtRegisterMsg.Foreground = System.Windows.Media.Brushes.DarkRed;

        if (GrdUsers.SelectedItem is not UserListRow row)
        {
            TxtRegisterMsg.Text = "목록에서 계정을 선택하세요.";
            return;
        }

        int actorId = SessionContext.CurrentUser?.Id ?? 0;
        if (actorId == 0)
        {
            TxtRegisterMsg.Text = "세션 오류.";
            return;
        }

        bool newActive = !row.IsActive;
        if (!_databaseService.TrySetUserActive(row.Id, newActive, actorId, out string err))
        {
            TxtRegisterMsg.Text = err;
            return;
        }

        string actor = SessionContext.CurrentUser?.Username ?? "?";
        _databaseService.AppendEventLog(actor, null, null,
            $"계정 {(newActive ? "활성화" : "비활성화")}: {row.Username}");

        TxtRegisterMsg.Foreground = System.Windows.Media.Brushes.ForestGreen;
        TxtRegisterMsg.Text = $"{row.Username} → {(newActive ? "사용" : "비활성")}";
        RefreshGrid();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

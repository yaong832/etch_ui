using System.Text.RegularExpressions;

namespace etch_ui.Security;

public static class PasswordPolicy
{
    public static bool IsValid(string? password, out string message)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            message = "비밀번호를 입력하세요.";
            return false;
        }

        if (password.Length < 8)
        {
            message = "비밀번호는 최소 8자리여야 합니다.";
            return false;
        }

        if (!Regex.IsMatch(password, "[A-Za-z]") || !Regex.IsMatch(password, "[0-9]"))
        {
            message = "비밀번호는 영문과 숫자를 모두 포함해야 합니다.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

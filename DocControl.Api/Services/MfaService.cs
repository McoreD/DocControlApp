using System.Globalization;
using OtpNet;

namespace DocControl.Api.Services;

public sealed class MfaService
{
    private const string Issuer = "DocControl";

    public string GenerateSecret()
    {
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    public string BuildOtpAuthUri(string secret, string email)
    {
        var issuer = Uri.EscapeDataString(Issuer);
        var account = Uri.EscapeDataString(email);
        return $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits=6";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (!int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;

        var totp = new Totp(Base32Encoding.ToBytes(secret.Trim()), step: 30, totpSize: 6);
        // Allow slight clock skew
        return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
    }
}

using OSFR.Linux.Installer.Services;
using Xunit;

namespace Installer.UnitTests;

public class DiagnosticBundleServiceTests
{
    [Fact]
    public void Redact_removes_credentials_and_session_identifiers()
    {
        const string input = "SessionId=secret Password: hunter2 Authorization=BearerValue https://example.invalid/?token=abc123&safe=yes";
        var result = DiagnosticBundleService.Redact(input);

        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("BearerValue", result);
        Assert.DoesNotContain("abc123", result);
        Assert.Contains("safe=yes", result);
        Assert.Contains("<redacted>", result);
    }
}

using System.Security.Cryptography;
using System.Text;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Security;

public sealed class DashSpecAccessValidator(DashSpecAccessOptions options)
{
    public bool IsRequired => options.IsRequired;

    public bool Validate(string? provided)
    {
        if (!options.IsRequired)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(options.ApiKey);
        var actual = Encoding.UTF8.GetBytes(provided);
        return expected.Length == actual.Length &&
               CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

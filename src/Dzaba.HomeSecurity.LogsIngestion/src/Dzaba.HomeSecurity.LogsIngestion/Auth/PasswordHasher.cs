namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

internal interface IPasswordHasher
{
    Task<bool> VerifyPasswordAsync(string userName, string password);
}

internal sealed class PasswordHasher : IPasswordHasher
{
    public async Task<bool> VerifyPasswordAsync(string userName, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        throw new NotImplementedException();
    }
}

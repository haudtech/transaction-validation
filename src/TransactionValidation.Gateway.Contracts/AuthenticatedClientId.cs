namespace TransactionValidation.Gateway.Contracts;

public readonly record struct AuthenticatedClientId
{
    private AuthenticatedClientId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryParse(string? value, out AuthenticatedClientId clientId)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || value.Any(char.IsControl)
            || value.Any(char.IsWhiteSpace))
        {
            clientId = default;
            return false;
        }

        clientId = new AuthenticatedClientId(value);
        return true;
    }
}

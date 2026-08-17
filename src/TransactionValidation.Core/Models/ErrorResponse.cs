namespace TransactionValidation.Core.Models;

/// <summary>
/// Aggregated error payload used to describe validation and business-rule failures in a structured response.
/// </summary>
public sealed class ErrorResponse
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public List<FieldError> Errors { get; init; } = [];
}

/// <summary>
/// Describes a single validation problem for a specific request field.
/// </summary>
public sealed class FieldError
{
    public required string Field { get; init; }

    public required string Message { get; init; }
}

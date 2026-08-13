namespace TransactionValidation.Core.Models;

public sealed class ErrorResponse
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public List<FieldError> Errors { get; init; } = [];
}

public sealed class FieldError
{
    public required string Field { get; init; }

    public required string Message { get; init; }
}

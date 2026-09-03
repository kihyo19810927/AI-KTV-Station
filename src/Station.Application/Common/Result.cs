namespace Station.Application.Common;

public readonly record struct Result<T>
{
    private readonly T? value;

    private Result(T value) { this.value = value; Error = Error.None; IsSuccess = true; }
    private Result(Error error) { value = default; Error = error; IsSuccess = false; }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => error == Error.None
        ? throw new ArgumentException("A failure requires an error.", nameof(error))
        : new(error);
}

namespace Qoot.Api.Infrastructure;

/// <summary>
/// An expected failure with a stable machine code and a localised message.
/// Turned into an RFC 7807 problem response by <see cref="ApiExceptionHandler"/>.
/// </summary>
public sealed class ApiError : Exception
{
    private ApiError(int status, string code, string message, IReadOnlyDictionary<string, string[]>? errors = null, object? details = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Errors = errors;
        Details = details;
    }

    public int Status { get; }
    public string Code { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
    public object? Details { get; }

    public static ApiError NotFound(string code = "not_found") => new(StatusCodes.Status404NotFound, code, Messages.Get(code));

    public static ApiError BadRequest(string code, params object[] args) => new(StatusCodes.Status400BadRequest, code, Messages.Get(code, args));

    public static ApiError Conflict(string code, object? details = null, params object[] args) =>
        new(StatusCodes.Status409Conflict, code, Messages.Get(code, args), details: details);

    public static ApiError Forbidden(string code = "forbidden") => new(StatusCodes.Status403Forbidden, code, Messages.Get(code));

    public static ApiError Unauthorized(string code = "unauthorized", params object[] args) => new(StatusCodes.Status401Unauthorized, code, Messages.Get(code, args));

    public static ApiError Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(StatusCodes.Status400BadRequest, "validation_failed", Messages.Get("validation_failed"), errors);

    public static ApiError Validation(string field, string code, params object[] args) =>
        Validation(new Dictionary<string, string[]> { [field] = [Messages.Get(code, args)] });
}

/// <summary>Collects field errors with localised messages, keyed by a JSON path such as <c>tiles[2].name.en</c>.</summary>
public sealed class FieldErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string path, string code, params object[] args)
    {
        if (!_errors.TryGetValue(path, out var list))
        {
            list = [];
            _errors[path] = list;
        }

        var message = Messages.Get(code, args);
        if (!list.Contains(message))
        {
            list.Add(message);
        }
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() => _errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal);

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw ApiError.Validation(ToDictionary());
        }
    }
}

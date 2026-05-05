namespace JsbaiBackend.Models;

/// <summary>
/// A standard wrapper for all API responses.
/// Every response the backend sends to the frontend will be wrapped in this.
/// This way the frontend always knows what shape the response will be.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public T? Data { get; set; }

    // Shortcut to create a success response
    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    // Shortcut to create a failure response
    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

// Non-generic version for responses that don't return data
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }

    public static ApiResponse Ok(string message = "OK") =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string error) =>
        new() { Success = false, Error = error };
}

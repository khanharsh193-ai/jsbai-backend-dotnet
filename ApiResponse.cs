namespace JsbaiBackend.Models;
public class ApiResponse<T> {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public T? Data { get; set; }
    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
public class ApiResponse {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public static ApiResponse Ok(string msg = "OK") => new() { Success = true, Message = msg };
    public static ApiResponse Fail(string error) => new() { Success = false, Error = error };
}

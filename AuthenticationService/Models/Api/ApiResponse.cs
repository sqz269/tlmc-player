namespace AuthenticationService.Models.Api;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Result { get; set; }

    public static ApiResponse<T> Ok(T @object)
    {
        return new ApiResponse<T>()
        {
            Success = true,
            Message = "success",
            Result = @object
        };
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Result = default
        };
    }
}
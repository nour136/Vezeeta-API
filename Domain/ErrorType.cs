namespace Domain
{
    public enum ErrorType
    {
        None = 0,
        ValidationError,
        Unauthorized,
        Forbidden,
        NotFound,
        Conflict,
        Unexpected
    }
}

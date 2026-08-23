using System.Net;

namespace Domain.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message, HttpStatusCode.Forbidden) { }
    }
}

using System.Net;

namespace Domain.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message, HttpStatusCode.BadRequest) { }
    }
}

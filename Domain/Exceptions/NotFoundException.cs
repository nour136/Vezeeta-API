using System.Net;

namespace Domain.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message) : base(message, HttpStatusCode.NotFound) { }
    }
}

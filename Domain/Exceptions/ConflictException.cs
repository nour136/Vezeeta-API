using System.Net;

namespace Domain.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message, HttpStatusCode.Conflict) { }
    }
}

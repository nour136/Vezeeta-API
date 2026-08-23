using System.Net;

namespace Domain.Exceptions
{
    public abstract class AppException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        protected AppException(string message, HttpStatusCode statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}

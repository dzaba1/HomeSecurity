using System.Net;

namespace Dzaba.HomeSecurity.LogsIngestion;

public class HttpResultException : Exception
{
	public HttpStatusCode HttpCode { get; }

	public HttpResultException(HttpStatusCode httpCode)
	{
        HttpCode = httpCode;
    }

	public HttpResultException(HttpStatusCode httpCode, string message) : base(message)
	{
        HttpCode = httpCode;
    }

	public HttpResultException(HttpStatusCode httpCode, string message, Exception inner) : base(message, inner)
	{
        HttpCode = httpCode;
    }
}

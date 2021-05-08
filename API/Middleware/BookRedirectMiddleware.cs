using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Middleware
{
    public class BookRedirectMiddleware
    {
        private readonly ILogger<BookRedirectMiddleware> _logger;

        public BookRedirectMiddleware(ILogger<BookRedirectMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            _logger.LogDebug("BookRedirect Path: {Path}", context.Request.Path.ToString());
            await next.Invoke(context);
        }
    }
}
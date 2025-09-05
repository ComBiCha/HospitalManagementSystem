namespace HMS.ApiGateway.Middleware;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Fix: Use indexer instead of Add to avoid duplicate key issues
        context.Request.Headers["X-Request-ID"] = requestId;
        context.Response.Headers["X-Request-ID"] = requestId;

        _logger.LogInformation("Gateway Request: {RequestId} {Method} {Path} from {RemoteIP}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway Error: {RequestId} - {Error}", requestId, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation("Gateway Response: {RequestId} {StatusCode} in {Duration}ms",
                requestId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
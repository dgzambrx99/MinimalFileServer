public class BasicAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _username;
    private readonly string _password;

    public BasicAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _username = configuration["BasicAuth:Username"];
        _password = configuration["BasicAuth:Password"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"MinimalFileServer\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        var authValue = authHeader.Replace("Basic ", "");
        var credentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authValue)).Split(':');

        if (credentials[0] == _username && credentials[1] == _password)
        {
            await _next(context);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
    }
}

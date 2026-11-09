var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/healthz");

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/transactions/v1/partner/transactions", StringComparison.OrdinalIgnoreCase)
        && !HttpMethods.IsPost(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        context.Response.Headers["Allow"] = HttpMethods.Post;
        return;
    }

    await next();
});

app.MapReverseProxy();

app.Run();

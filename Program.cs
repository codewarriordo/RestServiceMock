
using ServiceMock.Config;
using Serilog;
using Serilog.Templates;



var builder = WebApplication.CreateBuilder(args);



// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<Configurator>();

builder.Services.AddHttpClient("WebHook", httpClient =>
              {
                  httpClient.Timeout = TimeSpan.FromSeconds(15);
              });

//var logger  = new LoggerConfiguration().WriteTo.File(new ExpressionTemplate(
//      "[{@t:HH:mm:ss} {@l:u3} {SourceContext}] {@m}\n{@x}"),".\\Serilog.log", rollingInterval:RollingInterval.Day).CreateLogger();
var logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Logging.AddSerilog(logger);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
var configurator = app.Services.GetService<Configurator>();
configurator.Init();
foreach (ServiceMock.Config.Endpoint endpoint in configurator.BaseConfiguration.Endpoints)
{
    var method = "GetMethod";
    switch (endpoint.Method)
    {
        case RestMethod.GET:
            break;
        case RestMethod.POST:
            method = "PostMethod";
            break;
        case RestMethod.DELETE:
            method = "DeleteMethod";
            break;
    }
    app.MapControllerRoute(name: endpoint.Path,
                pattern: endpoint.Path,
                defaults: new { controller = "Base", action = method });
}

var httpLoggging = Environment.GetEnvironmentVariable("HTTP_LOGGING");
if (httpLoggging != null && httpLoggging.ToLower() == "true")
{
    logger.Warning("http logging is on ");
    app.UseHttpLogging();
}
app.Run();

using gradyn_api_2;
using gradyn_api_2.Services.BLL;
using gradyn_api_2.Services.DAL;

var builder = WebApplication.CreateBuilder(args);

// Begin services
builder.Services.AddSingleton<INextcloudClient, NextcloudClient>(); // Made this a singleton to avoid scope mismatch issues
builder.Services.AddSingleton<IGenericFormService, GenericFormService>();
// End services

// Begin CORS stuff
var corsSection = builder.Configuration.GetSection("Cors");

var allowedHosts = corsSection
    .GetSection("AllowedHosts")
    .Get<string[]>() ?? Array.Empty<string>();

var enforceProtocol = corsSection.GetValue<bool>("EnforceOriginProtocol");
var allowedSchemes = corsSection
    .GetSection("AllowedSchemes")
    .Get<string[]>() ?? Array.Empty<string>();

var hostMatcher = HostMatcher.Compile(
    allowedHosts,
    enforceProtocol,
    allowedSchemes
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => hostMatcher.IsAllowedOrigin(origin))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
// End CORS stuff

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
using gradyn_api_2.Services.BLL;
using gradyn_api_2.Services.DAL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<INextcloudClient, NextcloudClient>(); // Made this a singleton to avoid scope mismatch issues
builder.Services.AddSingleton<IGenericFormService, GenericFormService>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
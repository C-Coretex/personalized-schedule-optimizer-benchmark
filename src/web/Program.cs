using System.Text.Json.Serialization;
using Web.Features.Schedule.Endpoints.Generate;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<Handler>();

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

Web.Features.Schedule.Endpoints.Generate.Endpoint.Map(app);

app.Run();

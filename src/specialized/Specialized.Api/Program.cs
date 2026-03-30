var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddScoped<Specialized.Api.Features.Endpoints.Jobs.Run.Handler>();
builder.Services.AddScoped<Specialized.Api.Features.Endpoints.Jobs.Status.Handler>();
builder.Services.AddScoped<Specialized.Api.Features.Endpoints.Jobs.CurrentSolution.Handler>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

Specialized.Api.Features.Endpoints.Jobs.Run.Endpoint.Map(app);
Specialized.Api.Features.Endpoints.Jobs.Status.Endpoint.Map(app);
Specialized.Api.Features.Endpoints.Jobs.CurrentSolution.Endpoint.Map(app);

app.Run();
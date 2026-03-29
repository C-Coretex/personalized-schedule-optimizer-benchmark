var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<Specialized.Features.Endpoints.Jobs.Run.Handler>();
builder.Services.AddScoped<Specialized.Features.Endpoints.Jobs.Status.Handler>();
builder.Services.AddScoped<Specialized.Features.Endpoints.Jobs.CurrentSolution.Handler>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

Specialized.Features.Endpoints.Jobs.Run.Endpoint.Map(app);
Specialized.Features.Endpoints.Jobs.Status.Endpoint.Map(app);
Specialized.Features.Endpoints.Jobs.CurrentSolution.Endpoint.Map(app);

app.Run();
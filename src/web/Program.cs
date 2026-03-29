var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".ScheduleOptimizer.Session";
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<Web.Features.Schedule.Endpoints.Generate.Handler>();
builder.Services.AddScoped<Web.Features.Schedule.Endpoints.GetGenerated.Handler>();

Web.Providers.ServiceCollectionExtensions.RegisterScheduleOptimizationClients(builder.Services, builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSession();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

Web.Features.Schedule.Endpoints.Generate.Endpoint.Map(app);
Web.Features.Schedule.Endpoints.GetGenerated.Endpoint.Map(app);

app.Run();

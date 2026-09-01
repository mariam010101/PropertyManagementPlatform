using System.Text.Json.Serialization;
using PMP.Api.Infrastructure;
using PMP.Api.Seed;
using PMP.Api.Services;
using PMP.Modules.Auth.Extensions;
using PMP.Modules.Maintenance.Abstractions;
using PMP.Modules.Maintenance.Extensions;
using PMP.Modules.Property.Abstractions;
using PMP.Modules.Property.Extensions;
using PMP.Modules.Resident.Abstractions;
using PMP.Modules.Resident.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<DbSeeder>();

// Modular monolith: register each module's services + schema-bound DbContexts.
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddPropertyModule(builder.Configuration);
builder.Services.AddResidentModule(builder.Configuration);
builder.Services.AddMaintenanceModule(builder.Configuration);

// Cross-module composition at the root:
// - Property module depends on an occupancy provider owned by the Resident module.
// - Maintenance notifications are stubbed here until the Communication module ships.
builder.Services.AddScoped<IUnitOccupancyProvider>(sp => sp.GetRequiredService<UnitOccupancyProvider>());
builder.Services.AddScoped<INotificationService, LoggingNotificationService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Idempotent migrations + seed (roles, admin, sample data). Non-fatal: the app
// still starts so the user can bring up SQLite and retry.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

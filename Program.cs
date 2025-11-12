using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using FingerprintAttendanceSystem.Data;
using FingerprintAttendanceSystem.Services;
using FingerprintAttendanceSystem.BackgroundServices;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllersWithViews();

// Configurar DbContext con SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar autenticación con cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Registrar servicios
// MODO DESARROLLO: Cambiar entre servicio real y mock
// Para usar sin lector físico, descomentar la línea Mock y comentar la línea Real

// OPCIÓN 1: Servicio REAL (requiere hardware ZK9500 y DLLs)

//JRFUENTES builder.Services.AddScoped<IFingerprintService, FingerprintService>();

builder.Services.AddSingleton<IFingerprintService>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var logger = sp.GetRequiredService<ILogger<FingerprintService>>();
    return new FingerprintService(scopeFactory, logger);
});

// OPCIÓN 2: Servicio MOCK (para desarrollo sin hardware)
// builder.Services.AddScoped<IFingerprintService, MockFingerprintService>();

builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddHttpClient<IAppSheetService, AppSheetService>();

// Registrar servicio de sincronización en background
builder.Services.AddHostedService<SyncBackgroundService>();

// Configurar sesiones
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Inicializar base de datos
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        Log.Information("Base de datos inicializada correctamente");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error al inicializar la base de datos");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

Log.Information("Aplicación iniciada - Sistema de Control de Asistencia");

app.Run();

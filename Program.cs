using System.Threading.RateLimiting;
using AtiatHire.Data;
using AtiatHire.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- MVC (anti-forgery is validated on every POST) ---
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// --- Running behind Render's proxy: trust X-Forwarded-* so the app sees https and the real client IP ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy addresses are not fixed, and the service is only reachable through it.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- Keep login cookies and anti-forgery tokens valid across restarts when a persistent path is provided ---
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("atiat-hire");
}

// --- Database (SQLite for the prototype; swap the provider for SQL Server/PostgreSQL in production) ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// --- Options ---
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<StaffOptions>(builder.Configuration.GetSection("Staff"));

// --- Application services ---
builder.Services.AddScoped<HireRequestService>();
builder.Services.AddScoped<ConflictService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddSingleton<WhatsAppLinkBuilder>();

// --- Staff authentication (cookie) ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "atiat.staff";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

// --- Basic abuse protection for public forms and login ---
// Note: behind a reverse proxy, configure ForwardedHeaders so RemoteIpAddress is the client's address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forms", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();

// Refuse to start in production with the sample staff password.
// Refuse to start outside Development unless real staff credentials were supplied.
if (!app.Environment.IsDevelopment())
{
    var staff = app.Configuration.GetSection("Staff").Get<StaffOptions>() ?? new StaffOptions();
    const string sample = "ChangeMe!123";

    var problems = new List<string>();
    if (staff.Users.Count == 0)
        problems.Add("no staff user is configured");
    for (var i = 0; i < staff.Users.Count; i++)
    {
        var u = staff.Users[i];
        if (string.IsNullOrWhiteSpace(u.Username))
            problems.Add($"Staff__Users__{i}__Username is empty");
        if (string.IsNullOrEmpty(u.Password) || u.Password == sample || u.Password.Length < 10)
            problems.Add($"Staff__Users__{i}__Password is missing, is the sample password, or is shorter than 10 characters");
    }

    if (problems.Count > 0)
        throw new InvalidOperationException(
            "Staff credentials are not configured correctly: " + string.Join("; ", problems) + ". " +
            "Set the environment variables Staff__Users__0__Username and Staff__Users__0__Password " +
            "(double underscores, exact capitalisation) in your host's environment settings.");
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// TLS is terminated by Render's proxy, so only redirect locally.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Create the database and seed the fleet (and optional demo requests) on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db, app.Configuration.GetValue<bool>("Seed:DemoData"));
}

app.Run();

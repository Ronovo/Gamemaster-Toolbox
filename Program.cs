using GM_Toolbox.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using GM_Toolbox.Filters;
using GM_Toolbox.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
connectionString = ResolveSqlitePath(connectionString, builder.Environment.ContentRootPath);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Accounts are a username and password with no email, so there is nothing to confirm. A
// forgotten password is reset with the account's security question (SecurityQuestions).
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();
// Flow runs and card decks live in the session, so the next person on this browser starts clean.
builder.Services.ConfigureApplicationCookie(options => options.Events.OnSigningOut = context =>
{
    context.HttpContext.Session.Clear();
    return Task.CompletedTask;
});
// Every page needs a login unless it is marked [AllowAnonymous]: About, the login pages and portfolio login.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews(options => options.Filters.Add<OwnershipExceptionFilter>());
builder.Services.AddScoped<SecurityQuestions>();
builder.Services.AddScoped<OracleConfigurator>();
builder.Services.AddScoped<FlowConfigurator>();
builder.Services.AddScoped<SheetStorageService>();
builder.Services.AddSingleton<PortfolioLogin>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Creates or upgrades the database on startup, so a fresh clone runs without `dotnet ef database update`.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// Folds the write-ahead log (gmtoolbox.db-wal) back into the .db on shutdown, so a copy of the .db alone has every write.
app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not checkpoint the SQLite write-ahead log on shutdown.");
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// Off by default: the container serves plain HTTP, often behind a proxy that already redirects. Set ForceHttps only where the app holds the certificate.
if (app.Configuration.GetValue<bool>("ForceHttps"))
{
    // Browsers cache HSTS hard; see https://aka.ms/aspnetcore-hsts before raising its 30-day max-age.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// The test coverage reports the image builds. Like the About page, they are open to everyone.
var coverageDir = app.Configuration["CoverageDir"] ?? Path.Combine(app.Environment.ContentRootPath, "coverage");
if (Directory.Exists(coverageDir))
{
    app.UseFileServer(new FileServerOptions { FileProvider = new PhysicalFileProvider(coverageDir), RequestPath = "/coverage" });
}

app.UseRouting();

// Only the account pages this app offers are open. The rest of the Identity UI (two-factor sign-in, email,
// external logins, the personal data download) is closed off.
string[] managePages = ["/Index", "/ChangePassword", "/SetPassword", "/PersonalData", "/DeletePersonalData"];
string[] closedPages = ["/Identity/Account/LoginWith2fa", "/Identity/Account/LoginWithRecoveryCode"];
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var closed = closedPages.Any(page => path.StartsWithSegments(page, StringComparison.OrdinalIgnoreCase))
        || (path.StartsWithSegments("/Identity/Account/Manage", StringComparison.OrdinalIgnoreCase, out var rest)
            && rest.HasValue && rest.Value != "/"
            && !managePages.Any(page => rest.StartsWithSegments(page, StringComparison.OrdinalIgnoreCase)));
    if (closed)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseAuthorization();

app.UseSession();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// Anchors a relative "Data Source" to the content root instead of the working directory, so the database is
// always App_Data/gmtoolbox.db wherever the app was launched from, and makes sure its folder exists.
static string ResolveSqlitePath(string connectionString, string contentRootPath)
{
    var csb = new SqliteConnectionStringBuilder(connectionString);

    if (string.IsNullOrWhiteSpace(csb.DataSource) || csb.DataSource == ":memory:")
    {
        return connectionString;
    }

    if (!Path.IsPathRooted(csb.DataSource))
    {
        csb.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, csb.DataSource));
    }

    var folder = Path.GetDirectoryName(csb.DataSource);
    if (!string.IsNullOrEmpty(folder))
    {
        Directory.CreateDirectory(folder);
    }

    return csb.ConnectionString;
}

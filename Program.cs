using GM_Toolbox.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using GM_Toolbox.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
connectionString = ResolveSqlitePath(connectionString, builder.Environment.ContentRootPath);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Account confirmation is off because there is nothing to confirm with: no IEmailSender is
// registered, so a required confirmation would strand every new account on a "check your email"
// page for mail that is never sent. Nothing is gated on being signed in yet either -- no
// [Authorize], no owner column on any oracle -- so an account is currently just a name. Turn
// this back on at the same time as wiring up a real mail sender, not before.
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<OracleConfigurator>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Create or upgrade the database file on startup. SQLite is a file, not a server, so
// there is nothing to install and nothing to connect to: a fresh clone can be run
// straight away instead of needing `dotnet ef database update` first. That matters for
// a tool meant to be self-hosted by anyone who wants their own copy.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// Fold the write-ahead log back into the .db on the way out.
//
// EF enables WAL journalling, so recent writes live in a gmtoolbox.db-wal sidecar. Copying
// only the .db after an un-checkpointed run yields an empty toolbox — which breaks the one
// thing this app is built around. A killed process cannot be helped here; the -wal must
// then travel with the .db (see README).
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
        // Never let a tidy-up step turn a clean shutdown into a crash.
        app.Logger.LogWarning(ex, "Could not checkpoint the SQLite write-ahead log on shutdown.");
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// HTTPS is opt-in and off by default, because the default deployment has no certificate to
// redirect to. The container serves plain HTTP on 8080, and the shape this is built for is a box
// on someone's own network reached by the people sitting in the room with them, so redirecting to
// a port nothing is listening on would turn `docker run` into a blank page. It is also the wrong
// behaviour behind a reverse proxy that terminates TLS: the proxy has already done the redirect,
// and the app only ever sees plain HTTP. Set ForceHttps only where the app itself holds the cert.
if (app.Configuration.GetValue<bool>("ForceHttps"))
{
    // Default max-age is 30 days, and localhost is excluded automatically. See
    // https://aka.ms/aspnetcore-hsts before raising it: HSTS is cached hard by browsers.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// Anchors a relative "Data Source" to the content root and makes sure its folder exists.
//
// SQLite resolves a relative path against the process working directory, which is not
// reliably the content root — IIS Express and `dotnet run` from a different folder both
// disagree with it. Left alone, that scatters stray .db files wherever the app happened
// to be launched from. Anchoring it means the database is always the same one file at
// App_Data/gmtoolbox.db, which is what makes "copy the file to move your instance"
// actually true. An absolute path in configuration is respected as-is.
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

    // CreateDirectory is a no-op when the folder already exists, so this cannot disturb
    // the export archives that also live under App_Data.
    var folder = Path.GetDirectoryName(csb.DataSource);
    if (!string.IsNullOrEmpty(folder))
    {
        Directory.CreateDirectory(folder);
    }

    return csb.ConnectionString;
}

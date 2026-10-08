using F9SDMS.Components;
using F9SDMS.Components.Account;
using F9SDMS.Data;
using F9SDMS.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddHostedService<StaleSessionCleanupService>();
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddSingleton<CheckFileStore>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
// Components that do background work (e.g. the dashboard's refresh timer) create
// short-lived contexts from this factory instead of sharing the circuit-wide context.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Re-check each login cookie's security stamp every minute (default is 30), so a demoted
// Admin or a user whose password was reset loses access quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

var app = builder.Build();

// Convert data from before tasks and two-level checking (only touches rows that still need it).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
        await ProjectChecking.BackfillAsync(db, clock.Now);
    }
    catch (Exception ex)
    {
        // Most likely the database hasn't been updated yet (run Update-Database).
        app.Logger.LogError(ex, "Converting project data for tasks failed.");
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// PDFs submitted for checking ("pdf") and checkers' marked-up PDFs ("markup"). Only admins,
// managers and people assigned to the project can open them.
app.MapGet("/checks/{id:int}/{kind}", async (int id, string kind, HttpContext http, ApplicationDbContext db, CheckFileStore store) =>
{
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var submission = await db.CheckSubmissions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    if (submission is null) return Results.NotFound();

    var allowed = http.User.IsInRole("Admin") || http.User.IsInRole("Manager")
        || await db.ProjectAssignments.AnyAsync(a => a.ProjectId == submission.ProjectId && a.EmployeeId == userId);
    if (!allowed) return Results.Forbid();

    string? storedName = null;
    string? fileName = null;
    if (kind == "pdf")
    {
        storedName = submission.PdfStoredName;
        fileName = submission.PdfFileName;
    }
    else if (kind == "markup")
    {
        storedName = submission.MarkupStoredName;
        fileName = submission.MarkupFileName;
    }

    var path = store.GetPath(storedName);
    if (path is null) return Results.NotFound();

    // "inline" opens the PDF in the browser tab instead of downloading it.
    var disposition = new ContentDispositionHeaderValue("inline");
    disposition.SetHttpFileName(fileName ?? "file.pdf");
    http.Response.Headers.ContentDisposition = disposition.ToString();
    return Results.File(path, "application/pdf", enableRangeProcessing: true);
}).RequireAuthorization();

// Files attached to a review: a checker's updated PDF or a marked-up PDF.
app.MapGet("/checks/review/{id:int}", async (int id, HttpContext http, ApplicationDbContext db, CheckFileStore store) =>
{
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var review = await db.CheckReviews.AsNoTracking().Include(r => r.Submission).FirstOrDefaultAsync(r => r.Id == id);
    if (review?.Submission is null) return Results.NotFound();

    var projectId = review.Submission.ProjectId;
    var allowed = http.User.IsInRole("Admin") || http.User.IsInRole("Manager")
        || await db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.EmployeeId == userId);
    if (!allowed) return Results.Forbid();

    var path = store.GetPath(review.FileStoredName);
    if (path is null) return Results.NotFound();

    var disposition = new ContentDispositionHeaderValue("inline");
    disposition.SetHttpFileName(review.FileName ?? "file.pdf");
    http.Response.Headers.ContentDisposition = disposition.ToString();
    return Results.File(path, "application/pdf", enableRangeProcessing: true);
}).RequireAuthorization();

app.Run();
using F9SDMS.Components;
using F9SDMS.Components.Account;
using F9SDMS.Data;
using F9SDMS.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddHostedService<DeadlineReminderService>();
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddSingleton<CheckFileStore>();
builder.Services.AddSingleton<TicketFileStore>();

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
// Queries that include several lists (e.g. a project's assignments and sub-categories) load each
// list separately instead of one big joined result: faster, and stops EF's warning about it.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
// Components that do background work (e.g. the dashboard's refresh timer) create
// short-lived contexts from this factory instead of sharing the circuit-wide context.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)), ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        // Password rules (also shown on the change-password pages): 8+ characters,
        // upper and lower case and a number. Symbols are allowed but not required.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Re-check each login cookie's security stamp every minute (default is 30), so a demoted
// Admin or a user whose password was reset loses access quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

// On the server the app sits behind Caddy, which handles HTTPS and passes requests on over plain
// HTTP. These headers tell the app the original request was HTTPS (for secure cookies and redirects).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Bring the database up to date and make sure the roles exist. On a brand-new database (the server),
// the first Admin is created from FirstAdmin:Email / FirstAdmin:Password and must choose a new
// password at first sign-in.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Manager", "Employee" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = app.Configuration["FirstAdmin:Email"];
    var adminPassword = app.Configuration["FirstAdmin:Password"];
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword) && !await userManager.Users.AnyAsync())
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = app.Configuration["FirstAdmin:FirstName"] ?? "Admin",
            LastName = app.Configuration["FirstAdmin:LastName"] ?? "",
            MustChangePassword = true
        };
        var created = await userManager.CreateAsync(admin, adminPassword);
        if (created.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
            app.Logger.LogWarning("Created the first Admin account {Email}.", adminEmail);
        }
        else
        {
            app.Logger.LogError("Could not create the first Admin: {Errors}", string.Join(" ", created.Errors.Select(e => e.Description)));
        }
    }
}

// Convert data from before tasks and two-level checking (only touches rows that still need it).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IAppClock>();
        await ProjectChecking.BackfillAsync(db, clock.Now);
        // Tasks made before deadlines existed get their project's due date.
        await TaskReminders.BackfillDeadlinesAsync(db);
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

// Calling UseAuthentication ourselves turns off the automatic sign-in setup, so UseAuthorization
// must be called here too, after it; otherwise pages check permissions before the login cookie
// is read and everyone is sent back to the sign-in page.
app.UseAuthentication();
app.UseAuthorization();

// Leftover Identity template pages the app doesn't use (email reset, two-factor, external logins,
// self-service account deletion...). They aren't linked anywhere; this keeps them unreachable.
// Accounts are managed by admins; people change their password at /Account/ChangePassword.
string[] blockedAccountPaths =
{
    "/Account/Manage", "/Account/ForgotPassword", "/Account/ResetPassword", "/Account/Register",
    "/Account/ConfirmEmail", "/Account/ResendEmailConfirmation", "/Account/ExternalLogin", "/Account/PerformExternalLogin",
    "/Account/LoginWith2fa", "/Account/LoginWithRecoveryCode", "/Account/InvalidPasswordReset"
};
app.Use(async (context, next) =>
{
    if (blockedAccountPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.Redirect("/");
        return;
    }
    await next();
});

// Someone signed in with a temporary password (new account or admin reset) must choose their own
// before opening any page. A deactivated account is signed out.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && HttpMethods.IsGet(context.Request.Method))
    {
        var path = context.Request.Path;
        var skip = path.StartsWithSegments("/Account") || path.StartsWithSegments("/_blazor")
            || path.StartsWithSegments("/_framework") || path.StartsWithSegments("/login")
            || Path.HasExtension(path.Value);
        if (!skip)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(context.User);
            var startupLog = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("F9SDMS.SignInCheck");
            if (user is null)
            {
                startupLog.LogWarning("Signed-in request to {Path} but no account matches user id {UserId}.",
                    path, userManager.GetUserId(context.User));
            }
            else if (user.DeactivatedAt is not null)
            {
                // Deactivated while signed in: end the login.
                startupLog.LogWarning("Signing out deactivated account {Email}.", user.Email);
                await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context, IdentityConstants.ApplicationScheme);
                context.Response.Redirect("/login");
                return;
            }
            else if (user.MustChangePassword)
            {
                context.Response.Redirect("/Account/SetNewPassword");
                return;
            }
        }
    }
    await next();
});

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

// Files attached to IT tickets: the ticket's employee and admins can open them; internal notes are admin-only.
app.MapGet("/tickets/file/{id:int}", async (int id, HttpContext http, ApplicationDbContext db, TicketFileStore store) =>
{
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var message = await db.TicketMessages.AsNoTracking().Include(m => m.Ticket).FirstOrDefaultAsync(m => m.Id == id);
    if (message?.Ticket is null) return Results.NotFound();

    var isAdmin = http.User.IsInRole("Admin");
    if (!isAdmin && (message.Ticket.EmployeeId != userId || message.IsInternal)) return Results.Forbid();

    var path = store.GetPath(message.AttachmentStoredName);
    if (path is null) return Results.NotFound();

    var fileName = message.AttachmentFileName ?? "attachment";
    var contentType = TicketFileStore.ContentTypeOf(fileName);
    var inline = contentType.StartsWith("image/") || contentType == "application/pdf";
    var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
    disposition.SetHttpFileName(fileName);
    http.Response.Headers.ContentDisposition = disposition.ToString();
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(path, contentType, enableRangeProcessing: true);
}).RequireAuthorization();

app.Run();
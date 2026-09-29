using System.Security.Claims;
using System.Text.Json;
using F9SDMS.Components.Account.Pages;
using F9SDMS.Components.Account.Pages.Manage;
using F9SDMS.Data;
using F9SDMS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            // Logout is exempt from the automatic antiforgery check. The token embedded in an
            // open dashboard tab is bound to whoever was signed in when that tab loaded, so it
            // stops matching as soon as a different account signs in (or the session changes)
            // in the same browser, which made logout fail with AntiforgeryValidationException.
            // Cross-site requests are still rejected below using the browser's
            // Sec-Fetch-Site / Origin headers.
            accountGroup.MapPost("/Logout", async (
     HttpContext context,
     ClaimsPrincipal user,
     SignInManager<ApplicationUser> signInManager,
     [FromServices] ApplicationDbContext dbContext,
     [FromServices] IAppClock clock,
     [FromForm] string? returnUrl) =>
            {
                if (!IsSameOriginRequest(context.Request))
                {
                    return Results.BadRequest();
                }

                var userId = signInManager.UserManager.GetUserId(user);
                if (userId is not null)
                {
                    // Close every open session for this user, not just the latest one,
                    // so no leftover session keeps them listed as active.
                    var openSessions = dbContext.AttendanceSessions
                        .Where(s => s.EmployeeId == userId && s.ClockOutTime == null)
                        .ToList();

                    foreach (var openSession in openSessions)
                    {
                        openSession.ClockOutTime = clock.Now;
                    }

                    if (openSessions.Count > 0)
                    {
                        await dbContext.SaveChangesAsync();
                    }
                }

                await signInManager.SignOutAsync();
                return Results.LocalRedirect($"~/{returnUrl}");
            }).DisableAntiforgery();

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }

        // Returns false when the browser tells us the request came from another site.
        private static bool IsSameOriginRequest(HttpRequest request)
        {
            var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
            if (!string.IsNullOrEmpty(fetchSite))
            {
                return string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase);
            }

            var origin = request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin))
            {
                return Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                    && string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Very old browsers send neither header; allow them rather than break logout.
            return true;
        }
    }
}

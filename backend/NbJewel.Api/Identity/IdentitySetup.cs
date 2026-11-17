// Role: Configures SQL Identity, secure cookies, CSRF, and an explicit development bootstrap command.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Businesses;

namespace NbJewel.Api.Identity;

public static class IdentitySetup
{
    public static void AddIdentityFoundation(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => SampleIdentityOptions.Read(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>()));
        builder.Services.AddHostedService<SampleIdentityBootstrap>();
        builder.Services.AddSingleton(services => new IdentityAvailability(services.GetRequiredService<SampleIdentityOptions>().Enabled ||
            !string.IsNullOrWhiteSpace(services.GetRequiredService<IConfiguration>()["NBJEWEL_IDENTITY_CONNECTION"])));
        // An unconfigured context has no provider and is never used for anonymous/public requests.
        builder.Services.AddDbContext<IdentityDatabase>((services, options) =>
        {
            var sample = services.GetRequiredService<SampleIdentityOptions>();
            var connection = services.GetRequiredService<IConfiguration>()["NBJEWEL_IDENTITY_CONNECTION"];
            if (sample.Enabled) options.UseInMemoryDatabase(sample.DatabaseName);
            else if (!string.IsNullOrWhiteSpace(connection)) options.UseSqlServer(connection);
        });
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<IdentityDatabase>().AddDefaultTokenProviders();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "nbjewel.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "nbjewel.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IPermissionAdapter, DatabasePermissionAdapter>();
        builder.Services.AddScoped<ErpBoundaryFilter>();
    }

    // Local CLI only. Never mapped to a controller or endpoint, and never changes account privileges.
    public static async Task<int> ResetOwnerPasswordAsync(WebApplication app)
    {
        if (!OwnerCommandAllowed(app.Configuration, app.Environment)) return 1;
        var email = app.Configuration["NBJEWEL_BOOTSTRAP_EMAIL"];
        var password = app.Configuration["NBJEWEL_BOOTSTRAP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return 1;
        try
        {
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            if (user is null || user.AccountType != "erp" || !user.AllStores)
            { Console.Error.WriteLine("Reset requires an existing all-store ERP owner. No account changed."); return 1; }
            var token = await users.GeneratePasswordResetTokenAsync(user);
            if (!(await users.ResetPasswordAsync(user, token, password)).Succeeded)
            { Console.Error.WriteLine("Owner reset failed Identity validation. No account changed."); return 1; }
            Console.WriteLine("Owner password reset. Existing session security stamps have been invalidated.");
            return 0;
        }
        catch (Exception)
        { Console.Error.WriteLine("Owner reset failed. Check private SQL configuration locally."); return 1; }
    }

    public static bool OwnerCommandAllowed(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() &&
        string.Equals(configuration["Repository:Provider"], "Sql", StringComparison.OrdinalIgnoreCase) &&
        !configuration.GetValue<bool>("SampleIdentity:Enabled") &&
        !configuration.GetValue<bool>("PublicDemo:Enabled") &&
        !string.IsNullOrWhiteSpace(configuration["NBJEWEL_IDENTITY_CONNECTION"]);

    public static async Task<int> BootstrapAsync(WebApplication app)
    {
        if (!OwnerCommandAllowed(app.Configuration, app.Environment))
        {
            Console.Error.WriteLine("Owner setup requires local Development, Sql, and disabled Sample identity/public demo.");
            return 1;
        }
        var email = app.Configuration["NBJEWEL_BOOTSTRAP_EMAIL"];
        var password = app.Configuration["NBJEWEL_BOOTSTRAP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Use the owner launcher to supply credentials privately.");
            return 1;
        }
        try
        {
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is not null)
            {
                Console.Error.WriteLine("Account exists. Setup never changes or elevates an existing account.");
                return 1;
            }
            // The reviewed schema must already exist. No create/migrate/seed operation is performed.
            var database = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
            await using var transaction = await database.Database.BeginTransactionAsync();
            var user = new ApplicationUser { Email = email, UserName = email, AccountType = "erp", AllStores = true, EmailConfirmed = true };
            if (!(await users.CreateAsync(user, password)).Succeeded)
            { Console.Error.WriteLine("Owner setup failed Identity validation."); return 1; }
            foreach (var permission in BusinessMemberships.OwnerPermissions)
                database.PermissionGrants.Add(new PermissionGrant { UserId = user.Id, Permission = permission });
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            Console.WriteLine("Created fixed demo ERP owner. Password and permissions will not change on startup.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Owner setup failed. Check the reviewed SQL schema and private configuration locally.");
            return 1;
        }
    }
}

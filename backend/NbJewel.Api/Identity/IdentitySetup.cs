// Role: Configures SQL Identity, secure cookies, CSRF, and an explicit development bootstrap command.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<int> BootstrapAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("Identity bootstrap is allowed only in Development.");
            return 1;
        }
        var email = Environment.GetEnvironmentVariable("NBJEWEL_BOOTSTRAP_EMAIL");
        var password = Environment.GetEnvironmentVariable("NBJEWEL_BOOTSTRAP_PASSWORD");
        using var scope = app.Services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<IdentityAvailability>().Configured ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Set NBJEWEL_IDENTITY_CONNECTION, NBJEWEL_BOOTSTRAP_EMAIL and NBJEWEL_BOOTSTRAP_PASSWORD.");
            return 1;
        }
        // The reviewed schema must already exist. This command never creates or migrates databases.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null)
        {
            Console.Error.WriteLine("Account exists. Bootstrap will not elevate an existing account.");
            return 1;
        }
        var database = scope.ServiceProvider.GetRequiredService<IdentityDatabase>();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var user = new ApplicationUser { Email = email, UserName = email, AccountType = "erp", AllStores = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) { Console.Error.WriteLine("Bootstrap failed Identity validation."); return 1; }
        foreach (var permission in new[] { "items.read", "items.write", "prices.read", "stock.read", "stock.write", "status.read", "status.write", "reference.read", "chat.read", "jewelry.manage", "knowledge.manage" })
            database.PermissionGrants.Add(new PermissionGrant { UserId = user.Id, Permission = permission });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("Created explicit all-store development ERP account. No business role mapping is assumed.");
        return 0;
    }
}

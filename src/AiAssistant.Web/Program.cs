using AiAssistant.Core.Configuration;
using AiAssistant.Core.Interfaces;
using AiAssistant.Infrastructure.Data;
using AiAssistant.Infrastructure.Services;
using AiAssistant.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();

builder.Services.Configure<AiSettings>(
    builder.Configuration.GetSection("AiSettings"));

var settings = builder.Configuration
    .GetSection("AiSettings")
    .Get<AiSettings>() ?? new AiSettings();

var dbPath = settings.DatabasePath;
var dbConnectionString = $"Data Source={dbPath}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(dbConnectionString));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(365);
    options.SlidingExpiration = true;
    options.Cookie.Name = "AiAssistant.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SaveTokens = true;
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

builder.Services.AddHttpClient<WebSearchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<Func<AppDbContext>>(sp =>
{
    var options = sp.GetRequiredService<DbContextOptions<AppDbContext>>();
    return () => new AppDbContext(options);
});

builder.Services.AddScoped<IConversationManager, ConversationService>();
builder.Services.AddScoped<IEmbeddingService, SimpleEmbeddingService>();
builder.Services.AddScoped<ISearchService, WebSearchService>();
builder.Services.AddScoped<IVectorStore>(sp => new SqliteVectorStore(dbPath));
builder.Services.AddScoped<IKnowledgeBase, KnowledgeService>();
builder.Services.AddScoped<IChatService, LocalAiEngine>();
builder.Services.AddScoped<ISelfLearner, SelfLearnerService>();
builder.Services.AddScoped<IEvolutionEngine, EvolutionEngine>();

builder.Services.AddSingleton<IHostedService>(sp =>
    new EvolutionBackgroundService(
        sp.GetRequiredService<ILogger<EvolutionBackgroundService>>(),
        dbConnectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/auth/google", async (HttpContext httpContext, SignInManager<IdentityUser> signInManager) =>
{
    if (string.IsNullOrEmpty(googleClientId))
        return Results.Redirect("/login");

    await httpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new()
    {
        RedirectUri = "/"
    });
    return Results.Empty;
});

app.MapGet("/auth/guest", async (SignInManager<IdentityUser> signInManager) =>
{
    var guestEmail = $"guest-{Guid.NewGuid():N}@local";
    var guestUser = new IdentityUser
    {
        UserName = "مهمان",
        Email = guestEmail,
        EmailConfirmed = true
    };

    var existingUser = await signInManager.UserManager.FindByEmailAsync(guestEmail);
    if (existingUser == null)
    {
        await signInManager.UserManager.CreateAsync(guestUser);
    }

    await signInManager.SignInAsync(guestUser ?? existingUser!, isPersistent: true);
    return Results.Redirect("/");
});

app.MapGet("/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapPost("/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.Run();

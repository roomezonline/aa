using AiAssistant.Core.Configuration;
using AiAssistant.Core.Interfaces;
using AiAssistant.Infrastructure.Data;
using AiAssistant.Infrastructure.Services;
using AiAssistant.Web.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    options.SaveTokens = true;
    options.Scope.Add("email");
    options.Scope.Add("profile");
});

builder.Services.AddHttpClient<WebSearchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<Func<AppDbContext>>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext>>();
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
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(App).Assembly);

app.MapPost("/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

app.MapGet("/login", () => Results.Challenge(new()
{
    RedirectUri = "/"
}, new[] { GoogleDefaults.AuthenticationScheme }));

app.Run();

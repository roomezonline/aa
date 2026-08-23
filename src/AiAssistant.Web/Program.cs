using AiAssistant.Core.Configuration;
using AiAssistant.Core.Interfaces;
using AiAssistant.Infrastructure.Data;
using AiAssistant.Infrastructure.Services;
using AiAssistant.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AiSettings>(
    builder.Configuration.GetSection("AiSettings"));

var settings = builder.Configuration
    .GetSection("AiSettings")
    .Get<AiSettings>() ?? new AiSettings();

builder.Services.AddHttpClient<WebSearchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<Func<AppDbContext>>(sp => () => new AppDbContext(settings.DatabasePath));

builder.Services.AddScoped<IConversationManager, ConversationService>();
builder.Services.AddScoped<IEmbeddingService, SimpleEmbeddingService>();
builder.Services.AddScoped<ISearchService, WebSearchService>();
builder.Services.AddScoped<IVectorStore>(sp => new SqliteVectorStore(settings.DatabasePath));
builder.Services.AddScoped<IKnowledgeBase, KnowledgeService>();
builder.Services.AddScoped<IChatService, LocalAiEngine>();
builder.Services.AddScoped<ISelfLearner, SelfLearnerService>();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

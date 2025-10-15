using LLMTextToSql.Agents;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using LLMTextToSql.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

if (args.Length > 0 && args[0].Equals("extract-cardgames-schema", StringComparison.OrdinalIgnoreCase))
{
    var config = builder.Configuration;
    string connString = config.GetConnectionString("CardGamesPostgres")
                        ?? throw new InvalidOperationException("Missing CardGamesPostgres");

    var extractor = new SchemaExtractorService(connString);
    string output = Path.Combine(builder.Environment.ContentRootPath, "Schemas", "card_games_schema.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await extractor.ExtractSchemaAsync(output);

    Console.WriteLine("Card‑games schema extraction complete.");
    return;
}


builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IFixerService, FixerService>();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
});


builder.Services.AddSingleton<IPythonSelectorAgent>(sp =>
    new PythonSelectorAgent(
        Path.Combine(builder.Environment.ContentRootPath, "Scripts/OpenAiAgents/Agents", "selector.py")
    )
);

builder.Services.AddSingleton<IPythonDecomposerAgent>(sp =>
    new PythonDecomposerAgent(
        Path.Combine(builder.Environment.ContentRootPath, "Scripts", "decomposer.py"),
        Path.Combine(builder.Environment.ContentRootPath, "Schemas", "pagila_compressed_schema.json")
    )
);

builder.Services.AddSingleton<IPythonRefinerAgent>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var cfg = sp.GetRequiredService<IConfiguration>();
    string sc = Path.Combine(env.ContentRootPath, "Scripts/OpenAiAgents/Agents", "refiner.py");
    string sch = Path.Combine(env.ContentRootPath, "Schemas", "card_games_schema.json");
    string dsn = cfg.GetConnectionString("CardGamesPostgres")
                 ?? throw new InvalidOperationException("Missing CardGamesPostgres");
    string pythonDsn = dsn
    .Replace("Host=", "host=")
    .Replace("Port=", "port=")
    .Replace("Database=", "dbname=")
    .Replace("Username=", "user=")
    .Replace("Password=", "password=")
    .Replace(";", " "); // replace ; with space for psycopg2

    return new PythonRefinerAgent(sc, sch, pythonDsn);
});

builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

using LLMTextToSql.Agents;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using LLMTextToSql.Services;

var builder = WebApplication.CreateBuilder(args);

if (args.Length > 0 && args[0].Equals("extract-schema", StringComparison.OrdinalIgnoreCase))
{
    // 1) Read connection string
    var config = builder.Configuration;
    string connString = config.GetConnectionString("PagilaPostgres")
                        ?? throw new InvalidOperationException("Missing PagilaPostgres");

    // 2) Run extractor
    var extractor = new SchemaExtractorService(connString);
    string output = Path.Combine(builder.Environment.ContentRootPath, "Schemas", "pagila_compressed_schema.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await extractor.ExtractSchemaAsync(output);

    Console.WriteLine("Schema extraction complete.");
    return;  // exit the app
}


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IFixerAgent, FixerAgent>();
builder.Services.AddSingleton<IAugmenterAgent, AugmenterAgent>();
builder.Services.AddSingleton<IInterpreterAgent, InterpreterAgent>();
builder.Services.AddSingleton<ISelectorAgent, SelectorAgent>();

builder.Services.AddSingleton<IPythonDecomposerAgent>(sp =>
    new PythonDecomposerAgent(
        pythonScriptPath: Path.Combine(builder.Environment.ContentRootPath, "Scripts", "decomposer.py"),
        schemaFilePath: Path.Combine(builder.Environment.ContentRootPath, "Schemas", "pagila_compressed_schema.json")
    )
);

builder.Services.AddSingleton<IPythonRefinerAgent>(sp =>
    new PythonRefinerAgent(
        pythonScriptPath: Path.Combine(builder.Environment.ContentRootPath, "Scripts", "refiner.py"),
        schemaFilePath: Path.Combine(builder.Environment.ContentRootPath, "Schemas", "pagila_compressed_schema.json")
    )
);


builder.Services.AddSingleton<IIterativeRefinerService>(sp =>
{
    // Pull the Postgres connection string from configuration or hard‐code it temporarily:
    var configuration = sp.GetRequiredService<IConfiguration>();
    string pgConnString = configuration.GetConnectionString("PagilaPostgres");
    var refinerAgent = sp.GetRequiredService<IPythonRefinerAgent>();
    return new IterativeRefinerService(refinerAgent, pgConnString);
});


builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});


var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

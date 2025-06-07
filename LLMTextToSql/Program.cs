using LLMTextToSql.Agents;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using LLMTextToSql.Services;

var builder = WebApplication.CreateBuilder(args);

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
    string pgConnString = configuration.GetConnectionString("PagilaPostgres")
                         ?? "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=pagila;";
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

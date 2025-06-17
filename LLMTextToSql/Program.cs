using LLMTextToSql.Agents;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using LLMTextToSql.Services;
using LLMTextToSql.Services.LLMTextToSql.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IFixerAgent, FixerAgent>();
builder.Services.AddSingleton<IAugmenterAgent, AugmenterAgent>();
builder.Services.AddSingleton<IInterpreterAgent, InterpreterAgent>();
builder.Services.AddSingleton<ISelectorAgent, SelectorAgent>();

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
    string sc = Path.Combine(env.ContentRootPath, "Scripts", "refiner.py");
    string sch = Path.Combine(env.ContentRootPath, "Schemas", "pagila_compressed_schema.json");
    string dsn = cfg.GetConnectionString("PagilaPostgres")
                 ?? throw new InvalidOperationException("Missing PagilaPostgres");
    return new PythonRefinerAgent(sc, sch, dsn);
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

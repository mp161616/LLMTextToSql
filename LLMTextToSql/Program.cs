using LLMTextToSql.Agents;
using LLMTextToSql.Interfaces.Agents;
using LLMTextToSql.Interfaces.Services;
using LLMTextToSql.Services;
using LLMTextToSql.Services.LLMTextToSql.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IFixerService, FixerService>();
builder.Services.AddSingleton<IAugmentService, AugmentService>();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
});


builder.Services.AddSingleton<IPythonSelectorAgent>(sp =>
    new PythonSelectorAgent(
        Path.Combine(builder.Environment.ContentRootPath, "Scripts", "selector.py")
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

using ExpenseManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title   = "Expense Management API",
        Version = "v1",
        Description = "REST API for the Expense Management system. " +
                      "Uses Azure SQL (Northwind) via Managed Identity authentication."
    });

    // Include XML comments if present
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// HttpClient for internal API calls (used by Razor Pages → API)
builder.Services.AddHttpClient("ExpenseApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/");
});

// ── App pipeline ────────────────────────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Swagger UI at /swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Expense Management API";
});

app.MapRazorPages();
app.MapControllers();

app.Run();

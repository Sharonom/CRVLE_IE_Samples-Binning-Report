using SamplesBucketing.Web.Data;
using SamplesBucketing.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// DB connection factory — connection string loaded from configuration.
// In Development, set via: dotnet user-secrets set ConnectionStrings:VortexConnection "<value>"
builder.Services.AddScoped<IDbConnectionFactory>(_ =>
{
    var cs = builder.Configuration.GetConnectionString("VortexConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException(
            "ConnectionStrings:VortexConnection is not configured. " +
            "In Development, run: dotnet user-secrets set ConnectionStrings:VortexConnection \"<your-connection-string>\"");
    return new SqlConnectionFactory(cs);
});

builder.Services.AddScoped<IVpoBinService, VpoBinService>();
builder.Services.AddScoped<IVpoListService, VpoListService>();
builder.Services.AddSingleton<ExcelExportService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

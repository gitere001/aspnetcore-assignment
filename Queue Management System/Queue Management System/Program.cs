using DotNetEnv;
// Load environment variables from .env file
Env.Load();
var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication("Cookies")
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
    });
builder.Services.AddScoped<Queue_Management_System.Data.DatabaseService>(); // Register DatabaseService
builder.Services.AddScoped<Queue_Management_System.Data.Repositories.ServiceRepository>();
builder.Services.AddScoped<Queue_Management_System.Data.Repositories.ServicePointRepository>();
builder.Services.AddScoped<Queue_Management_System.Data.Repositories.TicketRepository>();
builder.Services.AddScoped<Queue_Management_System.Data.Repositories.UserRepository>();
builder.Services.AddScoped<Queue_Management_System.Data.Repositories.DashboardRepository>();
builder.Services.AddScoped<Queue_Management_System.Data.DatabaseInitializer>();

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
app.UseAuthentication();

app.UseAuthorization();
using var scope = app.Services.CreateScope();
var initializer = scope.ServiceProvider.GetRequiredService<Queue_Management_System.Data.DatabaseInitializer>();
await initializer.InitializeAsync();
Console.WriteLine("✅ Database initialized");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

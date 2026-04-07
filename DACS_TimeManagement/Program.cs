using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DACS_TimeManagement.Data;
using DACS_TimeManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Chặn vòng lặp vô tận khi gửi dữ liệu qua Fetch/API
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = false; // Tắt thụt lề để giảm dung lượng file JSON
    });


builder.Services.AddSignalR(); // Đăng ký SignalR

// Add the database context to the services container
// Configure DbContext with resilient SQL Server options (retry + command timeout)
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine(">>> CONNECTION: " + defaultConn);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(defaultConn, sqlOptions =>
    {
        // Retry on transient failures
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        // Increase command timeout for long-running operations during startup/seeding
        sqlOptions.CommandTimeout(60);
    }));

// Add Identity services to the container with explicit options
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // Relax some defaults for development/testing convenience
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        // Do not require confirmed account for sign in in dev seed scenarios
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddRoleManager<RoleManager<IdentityRole>>();
// Add Razor Pages services to the container (for Identity UI)
builder.Services.AddRazorPages();

// Add TaskRepository to the services container
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ICalendarRepository, CalendarRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<DACS_TimeManagement.Hubs.NotificationHub>("/notificationHub");

app.MapRazorPages();

// Chạy DbSeeder để tạo Role và tài khoản Admin mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // GỌI HÀM Ở ĐÂY
        // Truyền 'services' (chính là IServiceProvider) vào hàm
        await DACS_TimeManagement.Data.DbSeeder.SeedData(services);

        Console.WriteLine(">>> Seed Data thành công!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Lỗi Seed Data: {ex.Message}");
    }
}

app.Run();

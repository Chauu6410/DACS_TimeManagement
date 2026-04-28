using DACS_TimeManagement.Data;
using DACS_TimeManagement.Hubs;
using DACS_TimeManagement.Models;
using DACS_TimeManagement.Repositories;
using DACS_TimeManagement.Services;
using DACS_TimeManagement.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. CẤU HÌNH CONTROLLERS & JSON (Chống vòng lặp vô tận)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = false;
    });

builder.Services.AddSignalR();
builder.Services.AddRazorPages();

// 2. CẤU HÌNH DATABASE
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(defaultConn, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60);
    }));

// 3. CẤU HÌNH IDENTITY (Hợp nhất - Chỉ đăng ký 1 lần duy nhất)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Cấu hình mật khẩu đơn giản để dễ Demo
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// 4. ĐĂNG KÝ CÁC REPOSITORIES & SERVICES
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ICalendarRepository, CalendarRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddScoped<IGoalService, GoalService>();

var app = builder.Build();

// 5. CẤU HÌNH HTTP PIPELINE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Thứ tự quan trọng: Authentication TRƯỚC Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<GoalHub>("/goalHub");
app.MapRazorPages();

// 6. CHẠY SEED DATA (Tự động tạo Role và Tài khoản mẫu)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedData(services);
        Console.WriteLine(">>> Seed Data thành công!");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, ">>> Lỗi Seed Data.");
    }
}

app.Run();
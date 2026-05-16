using System.Text.Json;
using IfsaKlasik.Web.Data;
using IfsaKlasik.Web.Hubs;
using IfsaKlasik.Web.Infrastructure;
using IfsaKlasik.Web.Models;
using IfsaKlasik.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SeedAdminOptions>(
    builder.Configuration.GetSection(SeedAdminOptions.SectionName));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Admin/Login";
    opts.AccessDeniedPath = "/Admin/Login";
    opts.Cookie.Name = "ifsak.admin";
});

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddJsonProtocol(opts =>
{
    opts.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.PayloadSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddScoped<IRoomCookieService, RoomCookieService>();
builder.Services.AddScoped<IGameRoomService, GameRoomService>();
builder.Services.AddHostedService<RoundExpirationHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);
    await IdentitySeeder.SeedAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<GameHub>("/hubs/game");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

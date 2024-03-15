using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
//builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
var app = builder.Build();

app.UseStaticFiles();

/*app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(Directory.GetCurrentDirectory(), "Views")),
        RequestPath = "/Views"
    });*/

app.UseRouting();

app.UseWebSockets();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<ChatHub>("/chathub");
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});
//app.MapGet("/", () => "Hello World!");

app.Run();

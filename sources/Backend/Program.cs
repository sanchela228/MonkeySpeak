using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Core.Database;
using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Configurations;
using Core.Websockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<App>(builder.Configuration.GetSection("App"));
builder.Services.Configure<Core.Configurations.BackendSettings>(builder.Configuration.GetSection("BackendSettings"));

builder.Services.AddRazorPages();

App.BuildDatabase(builder);

builder.Services.AddControllers();

var app = builder.Build();

var wsOpts = new Microsoft.AspNetCore.Builder.WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(60)
};
app.UseWebSockets(wsOpts);

app.Map("/connector", async (HttpContext ctx, IServiceProvider serviceProvider) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

    var backendSettings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Core.Configurations.BackendSettings>>().Value;
    var handler = new WebsocketMiddleware(ws, serviceProvider, Backend.Core.Context.Connections, Backend.Core.Context.Rooms, backendSettings);
    await handler.OpenWebsocketConnection(ctx);
});

Task.Run(RunUdpStunTest);


app.MapRazorPages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

await App.ApplyMigrations(app);
await SeedTestUsers(app);

app.Run();

static async Task SeedTestUsers(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userService = scope.ServiceProvider.GetRequiredService<Core.Database.Services.UserService>();

    try
    {
        var existing = await userService.GetUserByUsernameAsync("admin");
        if (existing is null)
        {
            await userService.CreateUserWithPasswordAsync("admin", "admin");
            Console.WriteLine("Seed: test user 'admin' created (password: admin)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed failed: {ex.Message}");
    }
}


static void RunUdpStunTest()
{
    Console.WriteLine("Starting UDP STUN server on port 3478...");
    using (UdpClient udp = new UdpClient(3478))
    {
        IPEndPoint remoteEP = null;
        Console.WriteLine("UDP STUN server started successfully on port 3478");

        while (true)
        {
            try
            {
                byte[] data = udp.Receive(ref remoteEP);
                Console.WriteLine($"Got request from {remoteEP}");

                string response = $"{remoteEP.Address}:{remoteEP.Port}";
                byte[] respBytes = Encoding.UTF8.GetBytes(response);

                int bytesSent = udp.Send(respBytes, respBytes.Length, remoteEP);
                Console.WriteLine($"Sent {bytesSent} bytes to {remoteEP}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
                remoteEP = null;
            }
        }
    }
}
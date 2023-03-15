global using Discord;
global using Discord.WebSocket;
global using Newtonsoft.Json;
global using Newtonsoft.Json.Linq;
global using Zomlib;
global using Zoom.Sources;
using Zoom;


var dclient = new DiscordSocketClient(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent });
dclient.Log += m => { Console.WriteLine(m); return Task.CompletedTask; };
await dclient.LoginAsync(TokenType.Bot, ZoomConfig.Instance.Get<string>("discordtoken"));
await dclient.StartAsync();

new MusicPlayer(dclient).StartListening();
Thread.Sleep(-1);
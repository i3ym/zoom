global using System.Collections.Immutable;
global using Discord;
global using Discord.WebSocket;
global using Newtonsoft.Json;
global using Newtonsoft.Json.Linq;
global using NLog;
global using Zomlib;
global using Zoom.Sources;
using Zoom;

DefaultLogging.Setup();
LogManager.GetLogger("Zoom").Info("Initializing zoom...");

Directory.CreateDirectory(ZoomConfig.Instance.Get<string>("cachepath"));
LogManager.GetCurrentClassLogger().Info($"Found {Directory.GetFiles(ZoomConfig.Instance.Get<string>("cachepath")).Length - 1} cached files");

var dclient = new DiscordSocketClient(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent });

var dsLogger = LogManager.GetLogger("Discord");
dclient.Log += m =>
{
    var severity = m.Severity switch
    {
        LogSeverity.Info => LogLevel.Info,
        LogSeverity.Warning => LogLevel.Warn,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Verbose => LogLevel.Trace,
        LogSeverity.Critical => LogLevel.Error,
        LogSeverity.Debug => LogLevel.Debug,
        _ => LogLevel.Info,
    };
    dsLogger.Log(severity, m.Exception, $"[{m.Source}] {m.Message}");

    return Task.CompletedTask;
};

await dclient.LoginAsync(TokenType.Bot, ZoomConfig.Instance.Get<string>("discordtoken"));
await dclient.StartAsync();

new MusicPlayer(dclient).StartListening();
Thread.Sleep(-1);
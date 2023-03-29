using Discord.Audio;

namespace Zoom;

public class GuildState
{
    readonly Logger Logger;

    public readonly SocketGuild Guild;
    public readonly SongQueue Queue = new SongQueue();
    public PlayState? CurrentState;
    public AudioOutStream? AudioStream;

    bool _Loop = false;
    public bool Loop
    {
        get => _Loop;
        set
        {
            _Loop = value;
            if (CurrentState is { }) CurrentState.Loop = value;
        }
    }

    public GuildState(SocketGuild guild)
    {
        Logger = LogManager.GetLogger($"Guild {guild.Id}");

        Guild = guild;
        Queue.OnAdd += async () =>
        {
            if (CurrentState is { }) return;

            var song = await Queue.Dequeue();
            if (song is null) return;

            _ = Play(song).ContinueWith(t =>
            {
                if (t.Exception is not null)
                    Logger.Error(t.Exception);
            });
        };
    }


    Task Play(Song song) => Play(song.Info, song.Stream);
    Task Play(SongInfo song, SoundStream stream) => Play(new PlayState(stream, song) { Loop = Loop });
    async Task Play(PlayState state)
    {
        await ReconnectIfNeeded();
        _ = Guild.AudioClient.SetSpeakingAsync(true);
        CurrentState = state;

        await state.Start(AudioStream!);

        if (await Queue.Dequeue() is { } song) _ = Play(song);
        else CurrentState = null;
    }

    public void Disconnect() => Guild.CurrentUser.VoiceChannel?.DisconnectAsync();
    ValueTask<bool> ReconnectIfNeeded() => ReconnectIfNeeded(Guild.CurrentUser.VoiceChannel);
    public async ValueTask<bool> ReconnectIfNeeded(SocketVoiceChannel? audioch)
    {
        if (AudioStream is { } && (Guild.AudioClient is null || Guild.AudioClient.ConnectionState == Discord.ConnectionState.Connected)) return true;
        if (audioch is null) return false;

        Logger.Info($"Connecting to voice chat {audioch.Id} '{audioch.Name}'");
        await audioch.ConnectAsync(selfDeaf: true);
        AudioStream = Guild.AudioClient.CreatePCMStream(AudioApplication.Music);

        if (CurrentState is { })
        {
            CurrentState.Reconnect(AudioStream);
            CurrentState.IsPaused = false;
        }

        Guild.AudioClient.Disconnected += _ => Task.Run(() =>
        {
            if (Guild.CurrentUser.VoiceChannel is { } && audioch.Id == Guild.CurrentUser.VoiceChannel.Id) return;

            Logger.Info($"Disconnected from voice channel {audioch.Id}");
            if (Guild.CurrentUser.VoiceChannel is null)
            {
                CurrentState?.Stop();
                CurrentState = null;

                return;
            }

            if (CurrentState is { }) CurrentState.IsPaused = true;
        });
        Guild.AudioClient.Connected += async () =>
        {
            AudioStream = null;

            if (CurrentState is { })
            {
                audioch = Guild.CurrentUser.VoiceChannel;

                await ReconnectIfNeeded();
                CurrentState.Reconnect(AudioStream!);
                CurrentState.IsPaused = false;
            }

            Logger.Info($"Reconnected to voice channel {audioch.Id}");
        };

        return true;
    }
}
using Zomlib.Commands;

namespace Zoom;

public class MusicPlayer
{
    static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    readonly DiscordSocketClient Client;
    readonly CommandList CommandList = new();
    readonly Dictionary<ulong, GuildState> States = new();

    public MusicPlayer(DiscordSocketClient client)
    {
        Client = client;
        CommandList.Add(CreateCommands());
    }

    public void StartListening()
    {
        Client.MessageReceived += async message =>
        {
            var prefixes = ImmutableArray.Create("!z ", "!z", "!zoom ");

            var text = message.Content;
            var prefix = prefixes.FirstOrDefault(p => text.StartsWith(p, StringComparison.Ordinal));
            if (prefix is null) return;

            Logger.Info($"({((SocketGuildChannel) message.Channel).Guild.Id} : {message.Channel.Id}) {message.Author.Username}: {message.Content}");
            text = text.Substring(prefix.Length);

            var exec = CommandList.TryExecute(text, new MessageInfo(text, new[] { message }), out _);
            var output = exec.Value ?? exec.Message;

            if (!string.IsNullOrWhiteSpace(output))
                await message.Channel.SendMessageAsync(output);
        };
    }


    IEnumerable<Command> CreateCommands()
    {
        var smcp = SocketMessageCommandParameter.Instance;
        var gcp = GuildCommandParameter.Instance;
        var gscp = new GuildStateCommandParameter(States);

        yield return new Command(SongPlaysNow, new[] { "info" }, "Информация о текущем треке", Parameters.From(gscp, info));
        yield return new Command(SongPlaysNow, new[] { "pause" }, "Пауза", Parameters.From(gscp, pause));
        yield return new Command(SongPlaysNow, new[] { "continue", "resume", "play", "unpause" }, "Продолжить трек", Parameters.From(gscp, cont));
        yield return new Command(SongPlaysNow, new[] { "seek" }, "Посикать на трек", Parameters.From(gscp, new TimeSpanCommandParameter("время", true), seek));
        yield return new Command(SongPlaysNow, new[] { "clear", "stop" }, "Очистить очередь", Parameters.From(gscp, clear));
        yield return new Command(SongPlaysNow, new[] { "disconnect", "dc" }, "Выйти из голосового", Parameters.From(gscp, disconnect));
        yield return new Command(SongPlaysNow, new[] { "skip" }, "Пропустить трек", Parameters.From(gscp, new IntCommandParameter("колво", false) { DefaultValue = 1 }, skip));
        yield return new Command(SongPlaysNow, new[] { "remove" }, "Убрать трек", Parameters.From(gscp, new IntCommandParameter("индекс", true) { Min = 0 }, remove));
        yield return new Command(SongPlaysNow, new[] { "move" }, "Передвинуть трек", Parameters.From(gscp, new IntCommandParameter("откуда", true) { Min = 0 }, new IntCommandParameter("куда", true) { Min = 0 }, move));
        yield return new Command(SongPlaysNow, new[] { "shuffle" }, "Перетасовать очередь", Parameters.From(gscp, shuffle));
        yield return new Command(SongPlaysNow, new[] { "queue", "q" }, "Очередь", Parameters.From(gscp, new IntCommandParameter("страница", false) { DefaultValue = 1 }, queue));

        yield return new Command(new[] { "uri", "url" }, "Запустить трек по ссылке", Parameters.From(smcp, new StringCommandParameter("ссылка", true), uri));
        yield return new Command(new[] { "mix" }, "Запустить трек с MixCloud", Parameters.From(smcp, new StringCommandParameter("поиск", true), mix));
        yield return new Command(new[] { "mixu" }, "Получить URL трека с MixCloud", Parameters.From(smcp, new StringCommandParameter("поиск", true), mixu));
        yield return new Command(new[] { "yt", "youtube" }, "Запустить трек с YouTube", Parameters.From(smcp, new StringCommandParameter("поиск", true), youtubeSearch));
        yield return new Command(new[] { "ya", "я" }, "Запустить яндекс радио", Parameters.From(smcp, new YandexRadioStationCommandParameter(false), yandex));
        yield return new Command(new[] { "gop", "гоп" }, "Гоп-фм", Parameters.From(smcp, gopfm));
        yield return new Command(new[] { "record" }, "Радио RECORD", Parameters.From(smcp, new NullableCommandParameter<int>(new IntCommandParameter("станция", false)), radioRecord));
        yield return new Command(new[] { "osu" }, "osu", Parameters.From(smcp, osu));

        yield return new Command(new[] { "fix" }, "fix", Parameters.From(gscp, fix));

        yield return new Command(new[] { "loop" }, "Залупить трек", Parameters.From(gscp, loop));
        yield return new Command(new[] { "help", "commands", "команды" }, "Список команд", Parameters.From(MessageInfoCommandParameter.Instance, new StringCommandParameter("фильтр", false), Commands));
        yield return new Command(new[] { "command", "команда" }, "Информация о команде", Parameters.From(new StringCommandParameter("команда", true), commandInfo));
    }

    string? Commands(MessageInfo info, string? filter)
    {
        var commands = CommandList.AsEnumerable();
        if (filter is not null)
        {
            filter = filter.ToLower();
            commands = commands.Where(cmd => cmd.Names.Any(x => x.Contains(filter, StringComparison.InvariantCultureIgnoreCase)));
        }

        commands = commands.Where(cmd => cmd.Parameters.PreCheck.Invoke(info));
        if (!commands.Any()) return "Команды не найдены";
        return "Кто прочитал: тот сдохнет" + Environment.NewLine + string.Join(Environment.NewLine, commands.Select(cmd => cmd.Names[0] + ": " + cmd.Description));
    }

    GuildState GetGuildState(SocketGuild guild)
    {
        if (States.TryGetValue(guild.Id, out var state)) return state;

        state = new GuildState(guild);
        States[guild.Id] = state;

        return state;
    }
    string GetInfo(SocketGuild guild) => GetInfo(GetGuildState(guild));
    string GetInfo(GuildState guild) =>
        guild.CurrentState is { } state
        ? GetInfo(state)
        : "Сейчас ничего не играет";
    static string GetInfo(PlayState play) => GetPlayerString(play);
    static string GetPlayerString(PlayState play)
    {
        const int length = 40;

        string progressBar, progressText;

        if (play.SongInfo.LengthSeconds == -1)
        {
            progressBar = new string('–', length);
            progressText = "∞";
        }
        else
        {
            var progress = (int) (play.ProgressSeconds * length / play.SongInfo.LengthSeconds);
            var load = (int) (play.LoadedSeconds * length / play.SongInfo.LengthSeconds);

            progressBar = new string('=', load) + new string('–', Math.Max(0, length - load));
            progressText = ToDurationString(play.ProgressSeconds) + " / " + ToDurationString(play.SongInfo.LengthSeconds);

            progressBar = progressBar.Substring(0, progress) + '■' + progressBar.Substring(progress);
        }

        var info = play.SongInfo.ToString();

        return $"""
            ```ocaml
            {new string(' ', Math.Max(0, length / 2 - info.Length / 2))}{info}
            {progressBar}
            {new string(' ', Math.Max(0, length / 2 - progressText.Length / 2 - 3))}{(play.IsPaused ? "|| " : "> ")}{progressText}
            ```
            """;
    }
    static string ToDurationString(long seconds) =>
        (seconds >= 60 * 60 ? (seconds / 60 / 60) + "ч " : string.Empty)
        + (seconds >= 60 ? ((seconds / 60) % 60) + "м " : string.Empty)
        + (seconds % 60) + "c";

    public static Song CreateUri(string uri, SongInfo info) => new Song(info, FileSoundStream.FromUrl(uri));

    OperationResult SongPlaysNow(MessageInfo info) =>
        GetGuildState(((SocketGuildChannel) info.Object<SocketMessage>().Channel).Guild).CurrentState is { }
        ? true
        : OperationResult.Err("Сейчас ничего не играет");


    string? info(GuildState guild) => GetInfo(guild);
    string? pause(GuildState guild)
    {
        if (guild.CurrentState is { } state) state.IsPaused = true;
        return GetInfo(guild);
    }
    string? cont(GuildState guild)
    {
        if (guild.CurrentState is { } state) state.IsPaused = false;
        return GetInfo(guild);
    }
    string? seek(GuildState guild, TimeSpan value)
    {
        if (guild.CurrentState is { } state) state.ProgressSeconds = (int) value.TotalSeconds;
        return "Сикаем в " + value + Environment.NewLine + GetInfo(guild);
    }
    string? clear(GuildState guild)
    {
        guild.Queue.Clear();
        if (guild.CurrentState is { } state) state.Stop();

        return "Очередь очищена";
    }
    string? disconnect(GuildState guild)
    {
        clear(guild);
        guild.Disconnect();
        return "Ну и пашол в жопу";
    }
    string? skip(GuildState guild, int value)
    {
        for (int i = 0; i < value - 1; i++) guild.Queue.RemoveTop();
        if (guild.CurrentState is { } state) state.Stop();

        return "Пропущено";
    }
    string? remove(GuildState guild, int index)
    {
        if (index == 0) return skip(guild, 1);
        if (!guild.Queue.TryRemoveAt(index - 1)) return "Неверная позиция";

        return "Убран трек под номером " + index;
    }
    string? move(GuildState guild, int from, int to)
    {
        if (from == 0) return "Нельзя двигать нулевой трек";
        if (!guild.Queue.TryMove(from - 1, to - 1)) return "Неверная позиция";

        return "Передвинут трек " + guild.Queue[to - 1].Info + " на позицию " + to;
    }
    string? shuffle(GuildState guild)
    {
        if (guild.Queue.Count == 0) return "Тасовать нечего а";

        var rnd = new Random();
        var queue = guild.Queue.OrderBy(_ => rnd.Next()).ToArray();
        guild.Queue.Clear();
        guild.Queue.Enqueue(queue);

        return "Перетасовано " + guild.Queue.Count + " треков";
    }
    string? queue(GuildState guild, int page)
    {
        const int pagesize = 10;
        if (guild.Queue.Count == 0) return info(guild);

        int pageCount = (int) Math.Ceiling((float) guild.Queue.Count / pagesize);
        if (page < 1 || page > pageCount) return "Страница не найдена";
        page--;

        var queue = guild.Queue.AsEnumerable().Skip(page * pagesize).Take(pagesize).Select(x => x.Info.ToString());
        if (guild.CurrentState is { } && page == 0) queue = queue.Prepend(guild.CurrentState.SongInfo.ToString());
        if (guild.Queue.Radio is { } radio && page == pageCount) queue = queue.Append(radio.Name);

        return "Очередь (" + guild.Queue.Count + " треков):"
            + Environment.NewLine + "```"
            + Environment.NewLine + string.Join(Environment.NewLine, queue.Select((x, i) => (i + page * pagesize) + ": " + x))
            + Environment.NewLine + "```"
            + Environment.NewLine + GetInfo(guild)
            + Environment.NewLine + "Страница " + (page + 1) + " из " + pageCount;
    }
    string? loop(GuildState guild)
    {
        guild.Loop = !guild.Loop;
        return "Залупа " + (guild.Loop ? "включена" : "выключена");
    }
    string? fix(GuildState guild)
    {
        if (guild.CurrentState is null) return "нечего фиксить тыдурак чтоли";

        var si = guild.CurrentState.SongInfo;
        var ss = guild.CurrentState.InputStream;
        var song = new Song(si, ss);

        var progress = guild.CurrentState.ProgressSeconds;
        guild.Queue.EnqueueOnTop(song);
        guild.CurrentState.Stop();

        guild.CurrentState.ProgressSeconds = progress;
        return "fxd.";
    }

    string? uri(SocketMessage message, string uri) => play(message,
        async state =>
        {
            if (uri.StartsWith("/")) return "Неверный URL";

            if (uri.StartsWith("https://music.yandex.ru/")) return enqueue(state, (await YandexMusic.ParseYandexMusicUrlAsync(uri)).Select(x => x.Song).ToArray());
            if (uri.Contains("youtube.com/") || uri.Contains("youtu.be")) return enqueue(state, YouTube.Url(uri));

            return enqueue(state, new[] { CreateUri(uri, Decoder.InfoFromUri(uri).Result) });
        });
    string? mix(SocketMessage message, string query) => play(message,
        async state =>
        {
            var searchres = await Mixcloud.Search(query);
            var res = searchres.Data[0];
            var url = await YtDlp.GetUrl(res.Url);

            var song = new Song(res.ToSongInfo(), FileSoundStream.FromUrl("mix_" + res.Slug, url));

            state.Queue.Enqueue(song);
            return song.Info.ToString();
        });
    string? mixu(SocketMessage message, string query)
    {
        var t = Task.Run(async () =>
        {
            var searchres = await Mixcloud.Search(query);
            var res = searchres.Data[0];
            var url = await YtDlp.GetUrl(res.Url);

            return url;
        });
        t.ContinueWith(t => message.Channel.SendMessageAsync(t.Result));

        return null;
    }
    string? youtubeSearch(SocketMessage message, string search) => play(message, state => enqueue(state, YouTube.Search(search)));
    string? yandex(SocketMessage message, string? station)
    {
        if (station is null) return "Выберите станцию";
        // if (station is null) return "Выберите станцию:" + Environment.NewLine + string.Join(Environment.NewLine, YandexMusic.Stations.Keys.Select(x => x[0] + x[1..].ToLowerInvariant()));
        // TODO: /\

        return playRadio(message, () => new YandexRadio(station));
    }
    string? gopfm(SocketMessage message) => radioRecord(message, 556);
    string? radioRecord(SocketMessage message, int? stationId)
    {
        var stations = RadioRecord.GetStations().Result;
        if (!stationId.HasValue) return "Введите id станции"; // TODO: show list?
        if (!stations.TryGetValue(stationId.Value, out var station)) return "Станция не найдена";
        // https://www.radiorecord.ru/api/station/history/?id=556

        return play(message, state => enqueue(state, new[] { new Song(new SongInfo(station.Title, -1), PassthroughSoundStream.FromUri(station.Stream128)) }));
    }
    string? osu(SocketMessage message)
    {
        const string dir = "/mnt/Windus/Program Files/osu!/Songs/";

        if (!Directory.Exists(dir)) return "пашол нахуй";
        return play(message, state =>
        {
            Task.Run(() =>
            {
                foreach (var (artist, title, path) in Directory.GetFiles(dir, "*.osu", SearchOption.AllDirectories).DistinctBy(Path.GetDirectoryName).Select(osuToPath).Where(x => x.HasValue).Select(x => x!.Value).OrderBy(_ => Guid.NewGuid()))
                    state.Queue.Enqueue(new Song(new SongInfo(artist, title, Decoder.InfoFromUri(path).Result.LengthSeconds), MemorySoundStream.FromUrl(path)));


                static (string artist, string title, string path)? osuToPath(string osuPath)
                {
                    var dir = Path.GetDirectoryName(osuPath)!;
                    string? title = null, artist = null, path = null;

                    foreach (var line in File.ReadLines(osuPath))
                    {
                        checkSet(ref title, line, "Title:");
                        checkSet(ref artist, line, "Artist:");
                        checkSet(ref path, line, "AudioFilename:");

                        if (title is { } && artist is { } && path is { }) return (artist, title, Path.Combine(dir, path));
                    }

                    return null;


                    static void checkSet(ref string? value, string text, string start)
                    {
                        if (value is { }) return;

                        if (text.StartsWith(start))
                            value = text.Substring(start.Length).Trim();
                    }
                }
            });
            return "sosu";
        });
    }

    string? play(SocketMessage message, Func<GuildState, string> play)
    {
        var t = Task.Run(async () =>
        {
            var guild = ((SocketGuildChannel) message.Channel).Guild;
            var state = GetGuildState(guild);

            if (!(await state.ReconnectIfNeeded(((Discord.WebSocket.SocketGuildUser) message.Author).VoiceChannel)))
                return "Ты в аудиоканал то зайди а";

            return play(GetGuildState(guild));
        });
        t.ContinueWith(t => message.Channel.SendMessageAsync(t.Result));

        return null;
    }
    string? play(SocketMessage message, Func<GuildState, Task<string>> play)
    {
        var guild = ((SocketGuildChannel) message.Channel).Guild;
        var state = GetGuildState(guild);

        var t = Task.Run(async () =>
        {
            if (!(await state.ReconnectIfNeeded(((Discord.WebSocket.SocketGuildUser) message.Author).VoiceChannel)))
                return "Ты в аудиоканал то зайди а";

            return await play(GetGuildState(guild));
        });
        t.ContinueWith(t => message.Channel.SendMessageAsync(t.Result));


        return null;
    }
    string? playRadio(SocketMessage message, Func<Radio> radioFunc) => play(message, state => { state.Queue.Radio = radioFunc(); return state.Queue.Radio.Name; });
    static string enqueue(GuildState state, OperationResult<Song[]> rsongs)
    {
        if (!rsongs) return rsongs.AsString();

        var songs = rsongs.Value;
        if (songs.Length == 0) return "Ничего не найдено";

        state.Queue.Enqueue(songs);
        return string.Join(Environment.NewLine, songs.Select((x, i) => "(" + (state.Queue.Count + i) + ") " + x.Info.ToString() + (x.Info.LengthSeconds == -1 ? null : " (" + ToDurationString(x.Info.LengthSeconds) + ")")));
    }


    string? commandInfo(string cmdstring) => string.Join(Environment.NewLine, CommandList.Where(x => x.Names.Contains(cmdstring)).Select(cmd => cmd.FullCommandString));
}

using Zomlib.Commands;

namespace Zoom;

public class MusicPlayer
{
    static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    readonly DiscordSocketClient Client;
    readonly CommandList CommandList = new();
    readonly Dictionary<ulong, GuildState> States = new();
    readonly ImmutableArray<IMusicSource> MusicSources;

    public MusicPlayer(DiscordSocketClient client)
    {
        Client = client;

        MusicSources = ImmutableArray.Create<IMusicSource>(new Mixcloud(), new YouTube());
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


        foreach (var source in MusicSources)
        {
            yield return new Command(new[] { $"{source.CommandName}" }, $"{source.Name}: поиск", Parameters.From(smcp, new StringCommandParameter("поиск", true), search));
            yield return new Command(new[] { $"{source.CommandName}now" }, $"{source.Name}: поиск, сейчас", Parameters.From(smcp, new StringCommandParameter("поиск", true), searchnow));
            yield return new Command(new[] { $"{source.CommandName}link" }, $"{source.Name}: прямая ссылка", Parameters.From(smcp, new StringCommandParameter("поиск", true), directurl));
            yield return new Command(new[] { $"{source.CommandName}url" }, $"{source.Name}: поиск", Parameters.From(smcp, new StringCommandParameter("поиск", true), url));
            yield return new Command(new[] { $"{source.CommandName}urlnow" }, $"{source.Name}: поиск, сейчас", Parameters.From(smcp, new StringCommandParameter("поиск", true), urlnow));


            string? enqueue(SocketMessage message, string query, bool search, bool now) => play(message, async state =>
            {
                var song = DataCache.TryGetCachedSong(source.Category, query);
                if (song is null)
                {
                    var sinfo = (await loadinfo(source.Category, query, search))[0];
                    song = new Song(new SongInfo(sinfo.info.Title, sinfo.info.LengthSec), CachingSoundStream.FromUrl(source.Category, sinfo.info.Id, sinfo.url));
                }

                if (now) state.Queue.EnqueueOnTop(song);
                else state.Queue.Enqueue(song);


                var infotext = now
                    ? "Играет следующим:"
                    : $"На позиции {state.Queue.Count - 1}:";

                var secuntil = now
                    ? state.CurrentState.SongInfo.LengthSeconds
                    : state.Queue.Take(state.Queue.Count - 1).Sum(t => t.Info.LengthSeconds);
                secuntil -= (int) state.CurrentState.ProgressSeconds;

                return $"{infotext} {song.Info} (через {TimeSpan.FromSeconds(secuntil)})";
            });

            string? search(SocketMessage message, string query) => enqueue(message, query, true, false);
            string? searchnow(SocketMessage message, string query) => enqueue(message, query, true, true);
            string? url(SocketMessage message, string query) => enqueue(message, query, false, false);
            string? urlnow(SocketMessage message, string query) => enqueue(message, query, false, true);

            string? directurl(SocketMessage message, string query)
            {
                Task.Run(async () =>
                {
                    var data = await loadinfo(source.Category, query, true);
                    return string.Join('\n', data.Select(d => d.url));
                }).ContinueWith(t => message.Channel.SendMessageAsync(t.Result));

                return null;
            };

            async Task<ImmutableArray<(SongDataInfo info, string url)>> loadinfo(string category, string url, bool search)
            {
                var results = await (search ? source.Search(url) : source.GetByUrl(url));
                foreach (var result in results)
                    DataCache.SetSongInfo(category, result.Id, new SongInfo(result.Title, result.LengthSec));

                var urls = await Task.WhenAll(results.Select(r => source.GetDirectUrl(r.Id)));
                return results.Zip(urls).ToImmutableArray();
            }
        }


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
        yield return new Command(SongPlaysNow, new[] { "queue", "q" }, "Очередь", Parameters.From(gscp, new StringCommandParameter("страница/поиск", false) { DefaultValue = "1" }, queue));

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
    static string ToDurationString(double seconds) =>
        (seconds >= 60 * 60 ? (seconds / 60 / 60) + "ч " : string.Empty)
        + (seconds >= 60 ? ((seconds / 60) % 60) + "м " : string.Empty)
        + (seconds % 60) + "c";

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
    string? queue(GuildState guild, string pageOrSearch)
    {
        const int pagesize = 10;
        if (guild.Queue.Count == 0) return info(guild);

        if (!int.TryParse(pageOrSearch, out var page))
        {
            return string.Join(Environment.NewLine,
                guild.Queue
                .Select((x, i) => (x, i))
                .Where(x => x.x.Info.Title?.Contains(pageOrSearch, StringComparison.OrdinalIgnoreCase) == true)
                .Select(x => $"{x.i}: {x.x.Info}")
            );
        }

        int pageCount = (int) Math.Ceiling((float) guild.Queue.Count / pagesize);
        if (page < 1 || page > pageCount) return "Страница не найдена";
        page--;

        var queue = guild.Queue.AsEnumerable().Skip(page * pagesize).Take(pagesize).Select(x => x.Info.ToString());
        if (guild.CurrentState is { } && page == 0) queue = queue.Prepend(guild.CurrentState.SongInfo.ToString());
        if (guild.Queue.Radio is { } radio && page == pageCount) queue = queue.Append(radio.Name);

        return "Очередь (" + guild.Queue.Count + " треков):"
            + Environment.NewLine + "```"
            + Environment.NewLine + string.Join(Environment.NewLine, queue.Select((x, i) => (i + (page == 0 ? 0 : 1) + page * pagesize) + ": " + x))
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
                    state.Queue.Enqueue(new Song(SongInfo.Create(artist, title, Decoder.InfoFromUri(path).Result.LengthSeconds), DirectSoundStream.FromFileConverted(path)));


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

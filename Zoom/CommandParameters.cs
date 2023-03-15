using Zomlib.Commands;

namespace Zoom;

public class YandexRadioStationCommandParameter : StringCommandParameter
{
    public YandexRadioStationCommandParameter(string cmdname, bool required) : base(cmdname, required) { }
    public YandexRadioStationCommandParameter(bool required) : base("станция", required) { }

    public override OperationResult Check(MessageInfo info, string value) => YandexMusic.GetStations().Result.ContainsValue(value);
    public override OperationResult<string> Conversion(MessageInfo info, string[] parameters, ref int index)
    {
        var c = base.Conversion(info, parameters, ref index);
        if (!c) return c;

        var parameter = c.Value.ToUpperInvariant();
        if (!YandexMusic.GetStations().Result.TryGetValue(parameter.ToUpperInvariant(), out var station)) return OperationResult.Err("Станция не найдена");

        return station;
    }
}

public class SocketMessageCommandParameter : CommandParameter<SocketMessage>
{
    public static readonly SocketMessageCommandParameter Instance = new();

    public override bool Hidden => true;

    private SocketMessageCommandParameter() : base("\\sckmsg\\", false) { }

    public override OperationResult Check(MessageInfo _, SocketMessage __) => true;
    public override OperationResult<SocketMessage> Conversion(MessageInfo info, string[] _, ref int __) => info.Object<SocketMessage>();
}
public class GuildCommandParameter : CommandParameter<SocketGuild>
{
    public static readonly GuildCommandParameter Instance = new();

    public override bool Hidden => true;

    private GuildCommandParameter() : base("\\guild\\", false) { }

    public override OperationResult Check(MessageInfo _, SocketGuild __) => true;
    public override OperationResult<SocketGuild> Conversion(MessageInfo info, string[] _, ref int __) => ((SocketGuildChannel) info.Object<SocketMessage>().Channel).Guild;
}
public class GuildStateCommandParameter : CommandParameter<GuildState>
{
    public override bool Hidden => true;
    readonly Dictionary<ulong, GuildState> States;

    public GuildStateCommandParameter(Dictionary<ulong, GuildState> states) : base("\\gstate\\", false) => States = states;

    public override OperationResult Check(MessageInfo _, GuildState __) => true;
    public override OperationResult<GuildState> Conversion(MessageInfo info, string[] _, ref int __)
    {
        var guild = ((SocketGuildChannel) info.Object<SocketMessage>().Channel).Guild;
        if (States.TryGetValue(guild.Id, out var state)) return state;

        state = new GuildState(guild);
        States[guild.Id] = state;

        return state;
    }
}
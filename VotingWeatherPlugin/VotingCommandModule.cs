using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using Qmmands;

namespace VotingWeatherPlugin;

public class VotingCommandModule : ACModuleBase
{
    private readonly VotingTime _votingTime;
    private readonly VotingWeather _votingWeather;

    public VotingCommandModule(VotingTime votingTime, VotingWeather votingWeather)
    {
        _votingTime = votingTime;
        _votingWeather = votingWeather;
    }

    [Command("t"), RequireConnectedPlayer]
    public void VoteTime(string choice)
    {
        _votingTime.CountVote(Client!, choice);
    }

    [Command("w"), RequireConnectedPlayer]
    public void VoteWeather(int choice)
    {
        _votingWeather.CountVote(Client!, choice);
    }
}

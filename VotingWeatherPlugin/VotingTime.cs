using System.Globalization;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace VotingWeatherPlugin;

public class VotingTime : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly WeatherManager _weatherManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly VotingWeatherConfiguration _configuration;
    private readonly List<ACTcpClient> _alreadyVoted = new();
    private readonly List<double> _allVotes = new();

    private bool _votingOpen = false;

    public VotingTime(VotingWeatherConfiguration configuration, WeatherManager weatherManager, EntryCarManager entryCarManager, IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _configuration = configuration;
        _weatherManager = weatherManager;
        _entryCarManager = entryCarManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.EnableVotingTime)
            return;
        
        await Task.Delay(_configuration.VotingIntervalMilliseconds / 2, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAsync(stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting time update");
            }
            finally
            {
                await Task.Delay(_configuration.VotingIntervalMilliseconds - _configuration.VotingDurationMilliseconds, stoppingToken);
            }
        }
    }

    internal void CountVote(ACTcpClient client, string time)
    {
        if (!_votingOpen)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "There is no ongoing time vote." });
            return;
        }

        if (!DateTime.TryParseExact(time, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid time format. Usage: /t 15:31." });
            return;
        }

        if (_alreadyVoted.Contains(client))
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "You voted already." });
            return;
        }

        _alreadyVoted.Add(client);

        _allVotes.Add(dateTime.TimeOfDay.TotalSeconds);

        client.SendPacket(new ChatMessage { SessionId = 255, Message = $"Your vote for {time} has been counted." });
    }

    private async Task UpdateAsync(CancellationToken stoppingToken)
    {
        _allVotes.Clear();
        _alreadyVoted.Clear();

        _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "Vote for next time with this format:" });
        _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = " /t 15:31" });

        _votingOpen = true;
        await Task.Delay(_configuration.VotingDurationMilliseconds, stoppingToken);
        _votingOpen = false;

        if (_allVotes.Count == 0)
        {
            _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"Time vote ended. Time will not change." });
            return;
        }
        
        var winner = _allVotes.Average();
        
        string winnerTime = TimeSpan.FromSeconds(winner).ToString(@"hh\:mm");
        
        _entryCarManager.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"Time vote ended. Next time: {winnerTime}" });

        _weatherManager.SetTime((int)winner);
    }
}

using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using TrafficAiPlugin.Configuration;
using TrafficAiPlugin.Shared;
using TrafficAiPlugin.Splines;

namespace TrafficAiPlugin;

public class EntryCarTrafficAi : IEntryCarTrafficAi
{
    public CarStatus Status { get; private set; } = new();
    public bool EnableCollisions { get; private set; } = true;

    public bool ForceLights { get; internal set; }

    public long LastActiveTime { get; internal set; }
    public bool HasUpdateToSend { get; set; }
    public int TimeOffset { get; internal set; }
    public byte SessionId { get; }
    public uint LastRemoteTimestamp { get; internal set; }
    public long LastPingTime { get; set; }
    public long LastPongTime { get; internal set; }
    public ushort Ping { get; internal set; }
    public DriverOptionsFlags DriverOptionsFlags { get; internal set; }
    public string LegalTyres { get; set; } = "";
    public bool IsSpectator { get; internal set; }
    public string Model { get; }
    public string Skin { get; }
    public int SpectatorMode { get; internal set; }
    public float Ballast { get; internal set; }
    public int Restrictor { get; internal set; }
    public string? FixedSetup { get; internal set; }
    public List<ulong> AllowedGuids { get; internal set; } = new();
    public bool AiControlled { get; set; } = false;
    public AiMode AiMode { get; set; } = AiMode.None;
    public string? AiName { get; set; }
    
    
    
    public int TargetAiStateCount { get; private set; } = 1;
    public byte[] LastSeenAiSpawn { get; }
    public byte[] AiPakSequenceIds { get; }
    public IAiState?[] LastSeenAiState { get; }
    public bool AiEnableColorChanges => DriverOptionsFlags.HasFlag(DriverOptionsFlags.AllowColorChange);
    public int AiIdleEngineRpm { get; set; } = 800;
    public int AiMaxEngineRpm { get; set; } = 3000;
    public float AiAcceleration { get; set; }
    public float AiDeceleration { get; set; }
    public float AiCorneringSpeedFactor { get; set; }
    public float AiCorneringBrakeDistanceFactor { get; set; }
    public float AiCorneringBrakeForceFactor { get; set; }
    public float AiSplineHeightOffsetMeters { get; set; }
    public int? AiMaxOverbooking { get; set; }
    public int AiMinSpawnProtectionTimeMilliseconds { get; set; }
    public int AiMaxSpawnProtectionTimeMilliseconds { get; set; }
    public int? MinLaneCount { get; set; }
    public int? MaxLaneCount { get; set; }
    public int AiMinCollisionStopTimeMilliseconds { get; set; }
    public int AiMaxCollisionStopTimeMilliseconds { get; set; }
    public float VehicleLengthPreMeters { get; set; }
    public float VehicleLengthPostMeters { get; set; }
    public int? MinAiSafetyDistanceMetersSquared { get; set; }
    public int? MaxAiSafetyDistanceMetersSquared { get; set; }
    public List<LaneSpawnBehavior>? AiAllowedLanes { get; set; }
    public float TyreDiameterMeters { get; set; }
    private readonly List<AiState> _aiStates = [];
    private Span<AiState> AiStatesSpan => CollectionsMarshal.AsSpan(_aiStates);
    
    private readonly Func<EntryCarTrafficAi, AiState> _aiStateFactory;
    private readonly TrafficAi _trafficAi;
    
    public delegate EntryCar Factory(string model, string? skin, byte sessionId);
    
    private readonly ACServerConfiguration _serverConfiguration;
    private readonly TrafficAiConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly AiSpline _aiSpline;

    public EntryCarTrafficAi(TrafficAiConfiguration configuration,
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        ACServerConfiguration serverConfiguration,
        Func<EntryCarTrafficAi, AiState> aiStateFactory,
        TrafficAi trafficAi,
        AiSpline aiSpline)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _serverConfiguration = serverConfiguration;
        _aiStateFactory = aiStateFactory;
        _trafficAi = trafficAi;
        _aiSpline = aiSpline;
        
        AiPakSequenceIds = new byte[entryCarManager.EntryCars.Length];
        LastSeenAiState = new IAiState[entryCarManager.EntryCars.Length];
        LastSeenAiSpawn = new byte[entryCarManager.EntryCars.Length];
        
        AiInit();
    }

    private void AiInit()
    {
        AiName = $"{_configuration.NamePrefix} {SessionId}";
        SetAiOverbooking(0);

        _configuration.PropertyChanged += OnConfigReload;
        OnConfigReload(_configuration, new PropertyChangedEventArgs(string.Empty));
    }

    private void OnConfigReload(object? sender, PropertyChangedEventArgs args)
    {
        AiSplineHeightOffsetMeters = _configuration.SplineHeightOffsetMeters;
        AiAcceleration = _configuration.DefaultAcceleration;
        AiDeceleration = _configuration.DefaultDeceleration;
        AiCorneringSpeedFactor = _configuration.CorneringSpeedFactor;
        AiCorneringBrakeDistanceFactor = _configuration.CorneringBrakeDistanceFactor;
        AiCorneringBrakeForceFactor = _configuration.CorneringBrakeForceFactor;
        TyreDiameterMeters = _configuration.TyreDiameterMeters;
        AiMinSpawnProtectionTimeMilliseconds = _configuration.MinSpawnProtectionTimeMilliseconds;
        AiMaxSpawnProtectionTimeMilliseconds = _configuration.MaxSpawnProtectionTimeMilliseconds;
        AiMinCollisionStopTimeMilliseconds = _configuration.MinCollisionStopTimeMilliseconds;
        AiMaxCollisionStopTimeMilliseconds = _configuration.MaxCollisionStopTimeMilliseconds;

        foreach (var carOverrides in _configuration.CarSpecificOverrides)
        {
            if (carOverrides.Model == Model)
            {
                if (carOverrides.SplineHeightOffsetMeters.HasValue)
                    AiSplineHeightOffsetMeters = carOverrides.SplineHeightOffsetMeters.Value;
                if (carOverrides.EngineIdleRpm.HasValue)
                    AiIdleEngineRpm = carOverrides.EngineIdleRpm.Value;
                if (carOverrides.EngineMaxRpm.HasValue)
                    AiMaxEngineRpm = carOverrides.EngineMaxRpm.Value;
                if (carOverrides.Acceleration.HasValue)
                    AiAcceleration = carOverrides.Acceleration.Value;
                if (carOverrides.Deceleration.HasValue)
                    AiDeceleration = carOverrides.Deceleration.Value;
                if (carOverrides.CorneringSpeedFactor.HasValue)
                    AiCorneringSpeedFactor = carOverrides.CorneringSpeedFactor.Value;
                if (carOverrides.CorneringBrakeDistanceFactor.HasValue)
                    AiCorneringBrakeDistanceFactor = carOverrides.CorneringBrakeDistanceFactor.Value;
                if (carOverrides.CorneringBrakeForceFactor.HasValue)
                    AiCorneringBrakeForceFactor = carOverrides.CorneringBrakeForceFactor.Value;
                if (carOverrides.TyreDiameterMeters.HasValue)
                    TyreDiameterMeters = carOverrides.TyreDiameterMeters.Value;
                if (carOverrides.MaxOverbooking.HasValue)
                    AiMaxOverbooking = carOverrides.MaxOverbooking.Value;
                if (carOverrides.MinSpawnProtectionTimeMilliseconds.HasValue)
                    AiMinSpawnProtectionTimeMilliseconds = carOverrides.MinSpawnProtectionTimeMilliseconds.Value;
                if (carOverrides.MaxSpawnProtectionTimeMilliseconds.HasValue)
                    AiMaxSpawnProtectionTimeMilliseconds = carOverrides.MaxSpawnProtectionTimeMilliseconds.Value;
                if (carOverrides.MinCollisionStopTimeMilliseconds.HasValue)
                    AiMinCollisionStopTimeMilliseconds = carOverrides.MinCollisionStopTimeMilliseconds.Value;
                if (carOverrides.MaxCollisionStopTimeMilliseconds.HasValue)
                    AiMaxCollisionStopTimeMilliseconds = carOverrides.MaxCollisionStopTimeMilliseconds.Value;
                if (carOverrides.VehicleLengthPreMeters.HasValue)
                    VehicleLengthPreMeters = carOverrides.VehicleLengthPreMeters.Value;
                if (carOverrides.VehicleLengthPostMeters.HasValue)
                    VehicleLengthPostMeters = carOverrides.VehicleLengthPostMeters.Value;
                
                AiAllowedLanes = carOverrides.AllowedLanes;
                MinAiSafetyDistanceMetersSquared = carOverrides.MinAiSafetyDistanceMetersSquared;
                MaxAiSafetyDistanceMetersSquared = carOverrides.MaxAiSafetyDistanceMetersSquared;
                MinLaneCount = carOverrides.MinLaneCount;
                MaxLaneCount = carOverrides.MaxLaneCount;
            }
        }
    }

    public void RemoveUnsafeStates()
    {
        foreach (var aiState in AiStatesSpan)
        {
            if (!aiState.Initialized) continue;

            foreach (var targetAiState in AiStatesSpan)
            {
                if (aiState != targetAiState
                    && targetAiState.Initialized
                    && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < _configuration.MinStateDistanceSquared
                    && (_configuration.TwoWayTraffic || Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0))
                {
                    aiState.Despawn();
                    Logger.Verbose("Removed close state from AI {SessionId}", SessionId);
                }
            }
        }
    }

    public void AiUpdate()
    {
        foreach (var aiState in AiStatesSpan)
        {
            aiState.Update();
        }
    }

    public void AiObstacleDetection()
    {
        foreach (var aiState in AiStatesSpan)
        {
            aiState.DetectObstacles();
        }
    }

    public AiState? GetBestStateForPlayer(CarStatus playerStatus)
    {
        AiState? bestState = null;
        float minDistance = float.MaxValue;

        foreach (var aiState in AiStatesSpan)
        {
            if (!aiState.Initialized) continue;

            float distance = Vector3.DistanceSquared(aiState.Status.Position, playerStatus.Position);

            if (_configuration.TwoWayTraffic)
            {
                if (distance < minDistance)
                {
                    bestState = aiState;
                    minDistance = distance;
                }
            }
            else
            {
                bool isBestSameDirection = bestState != null && Vector3.Dot(bestState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isCandidateSameDirection = Vector3.Dot(aiState.Status.Velocity, playerStatus.Velocity) > 0;
                bool isPlayerFastEnough = playerStatus.Velocity.LengthSquared() > 1;
                bool isTieBreaker = minDistance < _configuration.MinStateDistanceSquared &&
                                    distance < _configuration.MinStateDistanceSquared &&
                                    isPlayerFastEnough;

                // Tie breaker: Multiple close states, so take the one with min distance and same direction
                if ((isTieBreaker && isCandidateSameDirection && (distance < minDistance || !isBestSameDirection))
                    || (!isTieBreaker && distance < minDistance))
                {
                    bestState = aiState;
                    minDistance = distance;
                }
            }
        }

        return bestState;
    }

    public bool IsPositionSafe(int pointId)
    {
        ArgumentNullException.ThrowIfNull(_aiSpline);

        var ops = _aiSpline.Operations;
            
        foreach (var aiState in AiStatesSpan)
        {
            if (aiState.Initialized 
                && Vector3.DistanceSquared(aiState.Status.Position, ops.Points[pointId].Position) < aiState.SafetyDistanceSquared
                && ops.IsSameDirection(aiState.CurrentSplinePointId, pointId))
            {
                return false;
            }
        }

        return true;
    }

    public (AiState? AiState, float DistanceSquared) GetClosestAiState(Vector3 position)
    {
        AiState? closestState = null;
        float minDistanceSquared = float.MaxValue;
        
        foreach (var aiState in AiStatesSpan)
        {
            float distanceSquared = Vector3.DistanceSquared(position, aiState.Status.Position);
            if (distanceSquared < minDistanceSquared)
            {
                closestState = aiState;
                minDistanceSquared = distanceSquared;
            }
        }

        return (closestState, minDistanceSquared);
    }

    public void GetInitializedStates(List<AiState> initializedStates, List<AiState>? uninitializedStates = null)
    {
        foreach (var aiState in AiStatesSpan)
        {
            if (aiState.Initialized)
            {
                initializedStates.Add(aiState);
            }
            else
            {
                uninitializedStates?.Add(aiState);
            }
        }
    }
    
    public bool CanSpawnAiState(Vector3 spawnPoint, AiState aiState)
    {
        // Remove state if AI slot overbooking was reduced
        if (_aiStates.IndexOf(aiState) >= TargetAiStateCount)
        {
            aiState.Dispose();
            _aiStates.Remove(aiState);

            Logger.Verbose("Removed state of Traffic {SessionId} due to overbooking reduction", SessionId);

            if (_aiStates.Count == 0)
            {
                Logger.Verbose("Traffic {SessionId} has no states left, disconnecting", SessionId);
                _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
            }

            return false;
        }

        foreach (var state in AiStatesSpan)
        {
            if (state == aiState || !state.Initialized) continue;

            if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _configuration.StateSpawnDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    public void SetAiControl(bool aiControlled)
    {
        if (AiControlled == aiControlled) return;
        
        AiControlled = aiControlled;
        if (AiControlled)
        {
            Logger.Debug("Slot {SessionId} is now controlled by AI", SessionId);

            AiReset();
            _entryCarManager.BroadcastPacket(new CarConnected
            {
                SessionId = SessionId,
                Name = AiName
            });
            if (_configuration.HideAiCars)
            {
                _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                {
                    SessionId = SessionId,
                    Visible = CSPCarVisibility.Invisible
                });
            }
        }
        else
        {
            Logger.Debug("Slot {SessionId} is no longer controlled by AI", SessionId);
            if (_aiStates.Count > 0)
            {
                _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
            }

            if (_configuration.HideAiCars)
            {
                _entryCarManager.BroadcastPacket(new CSPCarVisibilityUpdate
                {
                    SessionId = SessionId,
                    Visible = CSPCarVisibility.Visible
                });
            }

            AiReset();
        }
    }

    public void SetAiOverbooking(int count)
    {
        if (AiMaxOverbooking.HasValue)
        {
            count = Math.Min(count, AiMaxOverbooking.Value);
        }

        if (count > _aiStates.Count)
        {
            int newAis = count - _aiStates.Count;
            for (int i = 0; i < newAis; i++)
            {
                _aiStates.Add(_aiStateFactory(this));
            }
        }

        TargetAiStateCount = count;
    }

    private void AiReset()
    {
        foreach (var state in AiStatesSpan)
        {
            state.Despawn();
        }
        _aiStates.Clear();
        _aiStates.Add(_aiStateFactory(this));
    }
    
    public bool GetPositionUpdateForCar(IEntryCar<IClient> toCar, out PositionUpdateOut positionUpdateOut)
    {
        CarStatus targetCarStatus;
        var toTargetCar = toCar.TargetCar;
        if (toTargetCar != null)
        {
            var toTargetCarAi = _trafficAi.GetAiCarBySessionId(toTargetCar.SessionId);
            if (toTargetCar.AiControlled && toTargetCarAi.LastSeenAiState[toCar.SessionId] != null)
            {
                targetCarStatus = toTargetCarAi.LastSeenAiState[toCar.SessionId]!.Status;
            }
            else
            {
                targetCarStatus = toTargetCar.Status;
            }
        }
        else
        {
            targetCarStatus = toCar.Status;
        }

        CarStatus status;
        if (AiControlled)
        {
            var aiState = GetBestStateForPlayer(targetCarStatus);

            if (aiState == null)
            {
                positionUpdateOut = default;
                return false;
            }

            if (LastSeenAiState[toCar.SessionId] != aiState
                || LastSeenAiSpawn[toCar.SessionId] != aiState.SpawnCounter)
            {
                LastSeenAiState[toCar.SessionId] = aiState;
                LastSeenAiSpawn[toCar.SessionId] = aiState.SpawnCounter;

                if (AiEnableColorChanges)
                {
                    toCar.Client?.SendPacket(new CSPCarColorUpdate
                    {
                        SessionId = SessionId,
                        Color = aiState.Color
                    });
                }
            }

            status = aiState.Status;
        }
        else
        {
            status = Status;
        }

        float distanceSquared = Vector3.DistanceSquared(status.Position, targetCarStatus.Position);
        if (TargetCar != null || distanceSquared > NetworkDistanceSquared)
        {
            if ((_sessionManager.ServerTimeMilliseconds - OtherCarsLastSentUpdateTime[toCar.SessionId]) < OutsideNetworkBubbleUpdateRateMs)
            {
                positionUpdateOut = default;
                return false;
            }

            OtherCarsLastSentUpdateTime[toCar.SessionId] = _sessionManager.ServerTimeMilliseconds;
        }

        positionUpdateOut = new PositionUpdateOut(SessionId,
            AiControlled ? AiPakSequenceIds[toCar.SessionId]++ : status.PakSequenceId,
            (uint)(status.Timestamp - toCar.TimeOffset),
            Ping,
            status.Position,
            status.Rotation,
            status.Velocity,
            status.TyreAngularSpeed[0],
            status.TyreAngularSpeed[1],
            status.TyreAngularSpeed[2],
            status.TyreAngularSpeed[3],
            status.SteerAngle,
            status.WheelAngle,
            status.EngineRpm,
            status.Gear,
            (_serverConfiguration.Extra.ForceLights || ForceLights)
                ? status.StatusFlag | CarStatusFlags.LightsOn
                : status.StatusFlag,
            status.PerformanceDelta,
            status.Gas);
        return true;
    }
}

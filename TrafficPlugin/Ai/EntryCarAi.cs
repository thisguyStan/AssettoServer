using System.ComponentModel;
using System.Numerics;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using TrafficPlugin.Ai.Splines;
using TrafficPlugin.Configuration;

namespace TrafficPlugin.Ai;

public class EntryCarAi : EntryCar
{
    public int TargetAiStateCount { get; private set; } = 1;
    public AiState?[] LastSeenAiState { get; }
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
    private readonly List<AiState> _aiStates = new List<AiState>();
    private readonly ReaderWriterLockSlim _aiStatesLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    
    public new delegate EntryCarAi Factory(string model, string? skin, byte sessionId);
    
    private readonly Func<EntryCarAi, AiState> _aiStateFactory;
    private readonly AiSpline _spline;
    private readonly AiParams _aiParams;
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;

    public EntryCarAi(string model, 
        string? skin, 
        byte sessionId, 
        Func<EntryCarAi, AiState> aiStateFactory, 
        SessionManager sessionManager, 
        ACServerConfiguration configuration, 
        AiParams aiParams,
        EntryCarManager entryCarManager, 
        AiSpline spline) : base(model, skin, sessionId, sessionManager, configuration, entryCarManager)
    {
        _aiParams = aiParams;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        
        _spline = spline;
        _aiStateFactory = aiStateFactory;
        _sessionManager = sessionManager;

        LastSeenAiState = new AiState[entryCarManager.EntryCars.Length];
            
        AiInit();
    }
    
    private void AiInit()
    {
        AiName = $"{_aiParams.NamePrefix} {SessionId}";
        SetAiOverbooking(0);

        _aiParams.PropertyChanged += OnConfigReload;
        OnConfigReload(_configuration, new PropertyChangedEventArgs(string.Empty));
    }

    private void OnConfigReload(object? sender, PropertyChangedEventArgs args)
    {
        AiSplineHeightOffsetMeters = _aiParams.SplineHeightOffsetMeters;
        AiAcceleration = _aiParams.DefaultAcceleration;
        AiDeceleration = _aiParams.DefaultDeceleration;
        AiCorneringSpeedFactor = _aiParams.CorneringSpeedFactor;
        AiCorneringBrakeDistanceFactor = _aiParams.CorneringBrakeDistanceFactor;
        AiCorneringBrakeForceFactor = _aiParams.CorneringBrakeForceFactor;
        TyreDiameterMeters = _aiParams.TyreDiameterMeters;
        AiMinSpawnProtectionTimeMilliseconds = _aiParams.MinSpawnProtectionTimeMilliseconds;
        AiMaxSpawnProtectionTimeMilliseconds = _aiParams.MaxSpawnProtectionTimeMilliseconds;
        AiMinCollisionStopTimeMilliseconds = _aiParams.MinCollisionStopTimeMilliseconds;
        AiMaxCollisionStopTimeMilliseconds = _aiParams.MaxCollisionStopTimeMilliseconds;

        foreach (var carOverrides in _aiParams.CarSpecificOverrides)
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
        _aiStatesLock.EnterReadLock();
        try
        {
            for (var i = 0; i < _aiStates.Count; i++)
            {
                var aiState = _aiStates[i];
                if (!aiState.Initialized) continue;

                for (var j = 0; j < _aiStates.Count; j++)
                {
                    var targetAiState = _aiStates[j];
                    if (aiState != targetAiState
                        && targetAiState.Initialized
                        && Vector3.DistanceSquared(aiState.Status.Position, targetAiState.Status.Position) < _aiParams.MinStateDistanceSquared
                        && (_aiParams.TwoWayTraffic || Vector3.Dot(aiState.Status.Velocity, targetAiState.Status.Velocity) > 0))
                    {
                        aiState.Despawn();
                        Logger.Verbose("Removed close state from AI {SessionId}", SessionId);
                    }
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public void AiUpdate()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                aiState.Update();
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public void AiObstacleDetection()
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            for (var i = 0; i < _aiStates.Count; i++)
            {
                var aiState = _aiStates[i];
                aiState.DetectObstacles();
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public AiState? GetBestStateForPlayer(CarStatus playerStatus)
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            AiState? bestState = null;
            float minDistance = float.MaxValue;

            for (var i = 0; i < _aiStates.Count; i++)
            {
                var aiState = _aiStates[i];
                if (!aiState.Initialized) continue;

                float distance = Vector3.DistanceSquared(aiState.Status.Position, playerStatus.Position);

                if (_aiParams.TwoWayTraffic)
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
                    bool isTieBreaker = minDistance < _aiParams.MinStateDistanceSquared &&
                                        distance < _aiParams.MinStateDistanceSquared &&
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
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }

    public bool IsPositionSafe(int pointId)
    {
        ArgumentNullException.ThrowIfNull(_spline);
        
        _aiStatesLock.EnterReadLock();
        try
        {
            var ops = _spline.Operations;
            
            for (var i = 0; i < _aiStates.Count; i++)
            {
                var aiState = _aiStates[i];
                if (aiState.Initialized 
                    && Vector3.DistanceSquared(aiState.Status.Position, ops.Points[pointId].Position) < aiState.SafetyDistanceSquared
                    && ops.IsSameDirection(aiState.CurrentSplinePointId, pointId))
                {
                    return false;
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }

        return true;
    }

    public (AiState? AiState, float DistanceSquared) GetClosestAiState(Vector3 position)
    {
        AiState? closestState = null;
        float minDistanceSquared = float.MaxValue;

        _aiStatesLock.EnterReadLock();
        try
        {
            foreach (var aiState in _aiStates)
            {
                float distanceSquared = Vector3.DistanceSquared(position, aiState.Status.Position);
                if (distanceSquared < minDistanceSquared)
                {
                    closestState = aiState;
                    minDistanceSquared = distanceSquared;
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }

        return (closestState, minDistanceSquared);
    }

    public void GetInitializedStates(List<AiState> initializedStates, List<AiState>? uninitializedStates = null)
    {
        _aiStatesLock.EnterReadLock();
        try
        {
            for (int i = 0; i < _aiStates.Count; i++)
            {
                if (_aiStates[i].Initialized)
                {
                    initializedStates.Add(_aiStates[i]);
                }
                else
                {
                    uninitializedStates?.Add(_aiStates[i]);
                }
            }
        }
        finally
        {
            _aiStatesLock.ExitReadLock();
        }
    }
    
    public bool CanSpawnAiState(Vector3 spawnPoint, AiState aiState)
    {
        _aiStatesLock.EnterUpgradeableReadLock();
        try
        {
            // Remove state if AI slot overbooking was reduced
            if (_aiStates.IndexOf(aiState) >= TargetAiStateCount)
            {
                _aiStatesLock.EnterWriteLock();
                try
                {
                    aiState.Despawn();
                    _aiStates.Remove(aiState);
                }
                finally
                {
                    _aiStatesLock.ExitWriteLock();
                }

                Logger.Verbose("Removed state of Traffic {SessionId} due to overbooking reduction", SessionId);

                if (_aiStates.Count == 0)
                {
                    Logger.Verbose("Traffic {SessionId} has no states left, disconnecting", SessionId);
                    _entryCarManager.BroadcastPacket(new CarDisconnected { SessionId = SessionId });
                }

                return false;
            }

            foreach (var state in _aiStates)
            {
                if (state == aiState || !state.Initialized) continue;

                if (Vector3.DistanceSquared(spawnPoint, state.Status.Position) < _aiParams.StateSpawnDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            _aiStatesLock.ExitUpgradeableReadLock();
        }
    }

    public void SetAiControl(bool aiControlled)
    {
        if (AiControlled != aiControlled)
        {
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
                if (_aiParams.HideAiCars)
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

                if (_aiParams.HideAiCars)
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
    }

    public override void SetAiOverbooking(int count)
    {
        _aiStatesLock.EnterUpgradeableReadLock();
        try
        {
            if (AiMaxOverbooking.HasValue)
            {
                count = Math.Min(count, AiMaxOverbooking.Value);
            }

            if (count > _aiStates.Count)
            {
                _aiStatesLock.EnterWriteLock();
                try
                {
                    int newAis = count - _aiStates.Count;
                    for (int i = 0; i < newAis; i++)
                    {
                        _aiStates.Add(_aiStateFactory(this));
                    }
                }
                finally
                {
                    _aiStatesLock.ExitWriteLock();
                }
            }

            TargetAiStateCount = count;
        }
        finally
        {
            _aiStatesLock.ExitUpgradeableReadLock();
        }
    }

    private void AiReset()
    {
        _aiStatesLock.EnterWriteLock();
        try
        {
            foreach (var state in _aiStates)
            {
                state.Despawn();
            }
            _aiStates.Clear();
            _aiStates.Add(_aiStateFactory(this));
        }
        finally
        {
            _aiStatesLock.ExitWriteLock();
        }
    }
    
    
    public override bool GetPositionUpdateForCar(EntryCar toCar, out PositionUpdateOut positionUpdateOut)
    {
        CarStatus targetCarStatus;
        var toTargetCar = toCar.TargetCar;
        if (toTargetCar != null)
        {
            if (toTargetCar.AiControlled && ((EntryCarAi) toTargetCar).LastSeenAiState[toCar.SessionId] != null)
            {
                targetCarStatus = ((EntryCarAi) toTargetCar).LastSeenAiState[toCar.SessionId]!.Status;
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
            (_configuration.Extra.ForceLights || ForceLights)
                ? status.StatusFlag | CarStatusFlags.LightsOn
                : status.StatusFlag,
            status.PerformanceDelta,
            status.Gas);
        return true;
    }
}

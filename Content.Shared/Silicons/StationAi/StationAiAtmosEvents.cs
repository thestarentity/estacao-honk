using Content.Shared.Atmos.Monitor.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Ação da IA para definir o MODO de um alarme de ar pelo menu radial (alt-clique).
/// <see cref="Mode"/> é o modo-alvo (Filtragem/Encher/Pânico). Pânico (sugar todo o ar) só
/// é permitido sob lawset hostil — o servidor reconfirma.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiAirAlarmModeEvent : BaseStationAiAction
{
    public AirAlarmMode Mode;
    public override float CpuCost => Mode == AirAlarmMode.Panic ? 75f : 0f;
}

/// <summary>
/// Ação da IA para fechar (emergência) ou abrir um firelock pelo menu radial. Disponível sob
/// qualquer lei. <see cref="Close"/> decidido no cliente a partir do estado da porta (toggle).
/// Abrir só vale se o firelock não estiver travado por perigo (pressão/temperatura) — nesse caso
/// ele reabre/refecha sozinho, então abrir não é uma arma.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiFirelockEvent : BaseStationAiAction
{
    public bool Close;
}

/// <summary>
/// Ação da IA num ALARME DE INCÊNDIO pelo menu radial: dispara (fecha) ou reseta (reabre) todos os
/// firelocks da rede/área daquele alarme de uma vez. Disponível sob qualquer lei. <see cref="Alert"/>
/// true = disparar (estado de perigo → fecha); false = resetar (volta ao normal → reabre).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiFireAlarmEvent : BaseStationAiAction
{
    public bool Alert;
}


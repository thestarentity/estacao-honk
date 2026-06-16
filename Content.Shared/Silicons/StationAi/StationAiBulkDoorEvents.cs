using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

// Eventos das ações em massa de portas, disparados pelo menu radial da IA
// (segurar Alt + clicar numa porta). São BaseStationAiAction, igual aos eventos
// por-porta (StationAiBoltEvent etc.), e chegam ao servidor via StationAiRadialMessage.
// O estado desejado (trancar/destrancar, eletrificar/não) é decidido no cliente a
// partir da porta clicada e carregado no evento, pra o rótulo do radial e a ação
// ficarem sempre consistentes (toggle).

/// <summary>
/// Tranca ou destranca as portas numa área ao redor da porta clicada.
/// Disponível sob qualquer lawset.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiBoltAreaEvent : BaseStationAiAction
{
    public override float CpuCost => 3f;
    public bool Bolted;
}

/// <summary>
/// Tranca ou destranca todas as portas da estação de uma vez (lockdown).
/// Só funciona sob um lawset hostil.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiBoltStationEvent : BaseStationAiAction
{
    public override float CpuCost => 75f;
    public bool Bolted;
}

/// <summary>
/// Eletrifica ou para de eletrificar as portas numa área ao redor da porta clicada.
/// Disponível sob qualquer lawset (a IA já pode eletrificar porta por porta).
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiElectrifyAreaEvent : BaseStationAiAction
{
    public override float CpuCost => 3f;
    public bool Electrified;
}

/// <summary>
/// Eletrifica ou para de eletrificar todas as portas da estação de uma vez.
/// Só funciona sob um lawset hostil.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiElectrifyStationEvent : BaseStationAiAction
{
    public override float CpuCost => 75f;
    public bool Electrified;
}

/// <summary>
/// Liga/desliga o acesso de emergência das portas numa área ao redor da porta clicada.
/// Disponível sob qualquer lawset.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiEmergencyAccessAreaEvent : BaseStationAiAction
{
    public override float CpuCost => 3f;
    public bool EmergencyAccess;
}

/// <summary>
/// Liga/desliga o acesso de emergência de todas as portas da estação de uma vez.
/// Só funciona sob um lawset hostil.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiEmergencyAccessStationEvent : BaseStationAiAction
{
    public override float CpuCost => 75f;
    public bool EmergencyAccess;
}

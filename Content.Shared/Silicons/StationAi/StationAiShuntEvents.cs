using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Ação da IA Malf no radial de uma APC hackeada: shuntar o cérebro para dentro dela.
/// Chega ao servidor via StationAiRadialMessage e é levantada na APC.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiApcShuntEvent : BaseStationAiAction
{
    public override float CpuCost => 50f;
}

/// <summary>
/// Ação instantânea concedida ao cérebro shuntado para VOLTAR ao núcleo.
/// Removida quando o núcleo é destruído (CoreLost).
/// </summary>
public sealed partial class StationAiReturnFromShuntEvent : InstantActionEvent
{
}

using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Marca o cérebro de uma IA Malf que está SHUNTADO dentro de uma APC hackeada
/// (Fase 3, Bloco 2 — core shunting). Enquanto presente, a IA é dormente: o radial
/// e todas as ações ficam bloqueados. A IA sobrevive à destruição do núcleo, mas
/// fica presa na APC. Mora na entidade-cérebro (a mesma de leis/CPU).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiShuntedComponent : Component
{
    /// <summary>APC onde o cérebro está escondido. O cérebro fica no container "station_ai_shunt_slot" dela.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HostApc;

    /// <summary>True quando o núcleo foi destruído enquanto shuntada: ela fica presa, sem poder voltar.</summary>
    [DataField, AutoNetworkedField]
    public bool CoreLost;
}

using Robust.Shared.GameStates;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Economia de CPU ("processing power") da IA Malf. Mora na entidade-cérebro da IA
/// (a mesma que carrega <see cref="StationAiHostileLawComponent"/> e as leis). A CPU
/// sobe sozinha por tick; cada APC hackeada aumenta a taxa de ganho. Toda ação do
/// radial debita o custo declarado em <c>BaseStationAiAction.CpuCost</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiCpuComponent : Component
{
    /// <summary>CPU disponível atual. Networked p/ o cliente cinzar ações e mostrar o alert.</summary>
    [DataField, AutoNetworkedField]
    public float Cpu;

    /// <summary>Teto de CPU. Evita banking infinito.</summary>
    [DataField, AutoNetworkedField]
    public float MaxCpu = 200f;

    /// <summary>Ganho base de CPU por segundo, sem nenhuma APC hackeada.</summary>
    [DataField]
    public float BaseRegen = 0.1f;

    /// <summary>Ganho adicional de CPU por segundo, por APC hackeada.</summary>
    [DataField]
    public float RegenPerApc = 0.2f;

    /// <summary>Quantas APCs hackeadas alimentam a taxa. Mantido pelo StationAiApcSystem.</summary>
    [DataField]
    public int HackedApcCount;

    /// <summary>Alert de HUD que mostra a % de CPU.</summary>
    [DataField]
    public ProtoId<AlertPrototype> CpuAlert = "StationAiCpu";

    /// <summary>Nº de níveis de severidade do alert (0..Levels-1). Casado com o YAML do alert.</summary>
    [DataField]
    public int AlertLevels = 5;
}

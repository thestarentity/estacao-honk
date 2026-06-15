using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Marca um borg que está sendo PILOTADO pela IA de estação (Fase 5A), no lado SHARED, para liberar
/// o MENU RADIAL da IA enquanto ela pilota — mas com ACESSO LIMITADO (só ações individuais/seguras,
/// nada de área ou estação). Networked para o cliente saber restringir o radial.
///
/// É separado do <c>StationAiHeldComponent</c> de propósito: NÃO queremos os comportamentos do olho
/// da IA no borg (bloqueio de interação, relay, etc.) — o borg continua um mob normal; só ganha o
/// alt-clique radial nas máquinas que enxerga. Adicionado/removido junto com o
/// <c>StationAiPilotedBorgComponent</c> (server) em <see cref="Content.Server.Silicons.StationAi.StationAiBorgSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StationAiPilotingComponent : Component;

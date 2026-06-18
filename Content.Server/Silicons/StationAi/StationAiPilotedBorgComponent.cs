using Content.Shared.Access;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Prototypes;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Marca um borg que está sendo PILOTADO pela IA de estação (Fase 5A). Server-only: guarda o que é
/// preciso para desfazer o controle (devolver a mente, remover a ação de voltar, desativar o chassi
/// se fomos nós que o ligamos). Adicionado em <c>StationAiBorgSystem.OnControlBorg</c> e removido ao
/// largar o borg, ao detoná-lo/destruí-lo ou se ele morrer.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiPilotedBorgComponent : Component
{
    /// <summary>
    /// A mente da IA que está visitando este borg (dona do cérebro no núcleo).
    /// </summary>
    [DataField]
    public EntityUid MindId;

    /// <summary>
    /// A ação "Voltar ao núcleo" concedida ao borg enquanto pilotado (para remover ao largar).
    /// </summary>
    [DataField]
    public EntityUid? LeaveAction;

    /// <summary>
    /// True se fomos nós que ativamos o chassi (borg estava desligado). Nesse caso, desligamos de
    /// volta ao largar — senão deixamos como estava.
    /// </summary>
    [DataField]
    public bool WeActivated;

    /// <summary>
    /// Tags de acesso originais do borg, salvas ao assumir (para restaurar ao largar). Enquanto a IA
    /// pilota, damos AllAccess ao borg (abre portas como a IA + a tag "Borg" volta a existir, então as
    /// torretas da IA, que exemptam Borg/BasicSilicon, param de mirá-lo).
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>>? SavedAccessTags;

    /// <summary>
    /// Estado original do AccessComponent.Enabled do borg (borg vazio tem acesso desligado).
    /// </summary>
    [DataField]
    public bool SavedAccessEnabled;

    /// <summary>
    /// Leis originais do borg, salvas ao assumir, para restaurar ao largar (Item 8). Enquanto a IA
    /// pilota, o borg passa a operar sob as leis ATUAIS da IA (snapshot no momento do controle).
    /// </summary>
    [DataField]
    public List<SiliconLaw>? SavedLaws;
}

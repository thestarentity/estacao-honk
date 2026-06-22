namespace Content.Shared.Silicons.Laws.Components;

/// <summary>
/// Adicionado ao cérebro da IA (StationAiBrainComponent) quando ela detecta uma tentativa
/// de upload de leis hostis no console de upload. Controla o período de graça antes de
/// a IA Malf poder hackear o console.
/// Só existe no servidor — não networked de propósito.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiUploadDefenseComponent : Component
{
    /// <summary>
    /// Tempo de jogo até o qual a IA ainda está no período de graça (imune ao hack).
    /// Configurado no servidor quando a defesa é ativada.
    /// </summary>
    public TimeSpan GraceUntil;

    /// <summary>
    /// Se a IA já recebeu o aviso de que o período de graça está acabando.
    /// Evita spam de mensagens de aviso.
    /// </summary>
    public bool WarnedGraceEnding;
}

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Guarda o cooldown de assumir/largar borg da IA de estação (Fase 5A). Fica no CÉREBRO da IA
/// (StationAiHeld) para persistir entre um controle e outro. Server-only.
/// </summary>
[RegisterComponent]
public sealed partial class StationAiBorgControlComponent : Component
{
    /// <summary>
    /// Antes deste instante, a IA não pode assumir outro borg (evita pular de borg em borg sem custo).
    /// </summary>
    [DataField]
    public TimeSpan NextControl;
}

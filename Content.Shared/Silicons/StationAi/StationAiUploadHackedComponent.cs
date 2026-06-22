using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Adicionado a um console de upload de leis quando a IA Malf o hackeia.
/// A presença deste componente indica que o console está comprometido.
/// O campo <c>HackedBy</c> é networked para que o cliente possa exibir o tell visual
/// e o radial possa mostrar o estado correto.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationAiUploadHackedComponent : Component
{
    /// <summary>
    /// Qual IA (cérebro) hackeou este console. Networked para o cliente mostrar o tell visual.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HackedBy;
}

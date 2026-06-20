using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.StationAi;

/// <summary>
/// Disparado pelo cliente quando a IA clica num ponto do mapa (radar ou monitor de
/// tripulação) para teletransportar seu olho (a entidade remota do núcleo) até a
/// coordenada clicada. O servidor identifica a IA pela sessão que enviou o evento.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiMoveEyeEvent : EntityEventArgs
{
    public NetCoordinates Target;

    public StationAiMoveEyeEvent(NetCoordinates target)
    {
        Target = target;
    }
}

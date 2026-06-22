using Content.Shared.Silicons.StationAi;

namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    // _xforms (SharedTransformSystem) já está injetado em StationAiSystem.cs — reutilizado aqui.

    private void InitializeMoveEye()
    {
        SubscribeNetworkEvent<StationAiMoveEyeEvent>(OnMoveEye);
    }

    private void OnMoveEye(StationAiMoveEyeEvent ev, EntitySessionEventArgs args)
    {
        // O ator anexado à sessão é o olho/held da IA. TryGetCore acha o núcleo a partir dele.
        if (args.SenderSession.AttachedEntity is not { } actor)
            return;

        // Acha o núcleo e a entidade remota (holograma/olho) que deve ser movida.
        if (!TryGetCore(actor, out var core) || core.Comp?.RemoteEntity is not { } remote)
            return;

        var coords = GetCoordinates(ev.Target);
        if (!coords.IsValid(EntityManager))
            return;

        // Segurança: o destino vem do cliente. Sem isto, um cliente modificado poderia teleportar o
        // olho da IA para qualquer mapa (centcomm, outra estação, etc.). Só permitimos mover o olho
        // DENTRO do mesmo mapa do núcleo da IA — que é onde o radar/monitor de tripulação opera.
        if (_xforms.ToMapCoordinates(coords).MapId != Transform(core.Owner).MapID)
            return;

        _xforms.SetCoordinates(remote, coords);
    }
}

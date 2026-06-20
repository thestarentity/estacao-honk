using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;

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
        // O ator anexado à sessão é o olho da IA (StationAiHeld dentro do núcleo).
        if (args.SenderSession.AttachedEntity is not { } actor)
            return;

        // Acha o núcleo e a entidade remota (holograma/olho) que deve ser movida.
        if (!TryGetCore(actor, out var core) || core.Comp?.RemoteEntity is not { } remote)
            return;

        var coords = GetCoordinates(ev.Target);
        if (!coords.IsValid(EntityManager))
            return;

        _xforms.SetCoordinates(remote, coords);
    }
}

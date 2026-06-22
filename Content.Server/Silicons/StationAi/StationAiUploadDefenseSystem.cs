using Content.Server.Silicons.Laws;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Timing;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Lógica de defesa contra upload de leis da IA Malf.
/// Quando a IA vira Malf, recebe um período de graça de 10 minutos durante o qual
/// tentativas de reescrever suas leis pelo console são interceptadas silenciosamente.
/// Após hackear o console (<see cref="StationAiUploadHackedComponent"/>), a proteção
/// passa a ser permanente enquanto o console estiver comprometido.
/// </summary>
public sealed partial class StationAiUploadDefenseSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationAiBulkDoorSystem _bulkDoor = default!;

    private float _accumulator;
    private const float TickInterval = 5f;

    /// <summary>
    /// Registra o período de graça no cérebro da IA logo após ela virar Malf.
    /// Chamado por <see cref="StationAiMalfRuleSystem"/> depois de aplicar o lawset.
    /// </summary>
    public void StampGrace(EntityUid brain)
    {
        var def = EnsureComp<StationAiUploadDefenseComponent>(brain);
        def.GraceUntil = _timing.CurTime + TimeSpan.FromMinutes(10);
        def.WarnedGraceEnding = false;
    }

    /// <summary>
    /// Retorna <c>true</c> se a tentativa de upload deve ser bloqueada.
    /// Condição: IA está sob lei hostil E (ainda está no período de graça OU o console está hackeado).
    /// </summary>
    public bool IsProtected(EntityUid brain, EntityUid console)
    {
        if (!_bulkDoor.IsUserUnderHostileLaw(brain))
            return false;

        if (!TryComp<StationAiUploadDefenseComponent>(brain, out var def))
            return false;

        return _timing.CurTime < def.GraceUntil || HasComp<StationAiUploadHackedComponent>(console);
    }

    /// <summary>
    /// Notifica a IA que uma tentativa de sobrescrever suas leis foi interceptada,
    /// permitindo que ela blefe normalmente para a tripulação.
    /// </summary>
    public void NotifyBluff(EntityUid brain)
    {
        _popup.PopupEntity(Loc.GetString("station-ai-upload-intercepted"), brain, brain, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<StationAiUploadDefenseComponent>();
        while (query.MoveNext(out var uid, out var def))
        {
            if (def.WarnedGraceEnding)
                continue;

            var remaining = def.GraceUntil - _timing.CurTime;

            // Período de graça já expirou ou ainda faltam mais de 2 minutos: pula.
            if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(2))
                continue;

            // Verifica se a IA ainda está sob lei hostil antes de avisar.
            if (!_bulkDoor.IsUserUnderHostileLaw(uid))
                continue;

            _popup.PopupEntity(Loc.GetString("station-ai-upload-grace-ending"), uid, uid, PopupType.LargeCaution);
            def.WarnedGraceEnding = true;
        }
    }
}

using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ação da IA Malf para hackear o console de upload de leis (alt-clique).
/// Quando bem-sucedida, marca o console com <see cref="StationAiUploadHackedComponent"/>,
/// impedindo uploads de leis hostis enquanto o console estiver comprometido.
/// A CPU é cobrada centralmente em <c>OnRadialMessage</c> — este handler NÃO cobra de novo.
/// </summary>
public sealed partial class StationAiUploadConsoleSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconLawUpdaterComponent, StationAiHackUploadConsoleEvent>(OnHackUploadConsole);
    }

    private void OnHackUploadConsole(EntityUid uid, SiliconLawUpdaterComponent comp, StationAiHackUploadConsoleEvent args)
    {
        // Idempotente: se já foi hackeado, ignora.
        if (HasComp<StationAiUploadHackedComponent>(uid))
            return;

        var hacked = EnsureComp<StationAiUploadHackedComponent>(uid);
        hacked.HackedBy = args.User;
        Dirty(uid, hacked);

        _appearance.SetData(uid, StationAiUploadConsoleVisuals.Compromised, true);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} hackeou o console de upload de leis {ToPrettyString(uid):target} (IA Malf).");

        _popup.PopupEntity(Loc.GetString("station-ai-upload-console-hacked"), uid, args.User, PopupType.Medium);
    }
}

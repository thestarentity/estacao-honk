using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Tools.Systems;

namespace Content.Server.Silicons.StationAi;

/// <summary>
/// Ação da IA Malf para hackear o console de upload de leis (alt-clique).
/// Quando bem-sucedida, marca o console com <see cref="StationAiUploadHackedComponent"/>,
/// impedindo uploads de leis hostis enquanto o console estiver comprometido.
/// A CPU é cobrada centralmente em <c>OnRadialMessage</c> — este handler NÃO cobra de novo.
///
/// Também lida com o contra-jogo da tripulação: usar um multitool no console comprometido
/// remove o hack e restaura o funcionamento normal dos uploads de leis.
/// </summary>
public sealed partial class StationAiUploadConsoleSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    // Delay do reparo: 3 segundos com multitool.
    private const float RepairDelay = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconLawUpdaterComponent, StationAiHackUploadConsoleEvent>(OnHackUploadConsole);

        // Contra-jogo: tripulação pode reparar o console comprometido com multitool.
        // StationAiUploadHackedComponent só existe quando o console está comprometido,
        // então este par (componente, evento) é livre de colisões por construção.
        SubscribeLocalEvent<StationAiUploadHackedComponent, InteractUsingEvent>(OnInteractUsingHacked);
        SubscribeLocalEvent<StationAiUploadHackedComponent, StationAiUploadRepairDoAfterEvent>(OnRepairDoAfter);
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

    /// <summary>
    /// Inicia o DoAfter de reparo quando o usuário usa um multitool no console comprometido.
    /// </summary>
    private void OnInteractUsingHacked(EntityUid uid, StationAiUploadHackedComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Requer multitool (qualidade Pulsing).
        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            uid,
            RepairDelay,
            SharedToolSystem.PulseQuality,
            new StationAiUploadRepairDoAfterEvent());
    }

    /// <summary>
    /// Conclui o reparo: remove o hack, limpa o visual e avisa o usuário.
    /// </summary>
    private void OnRepairDoAfter(EntityUid uid, StationAiUploadHackedComponent comp, StationAiUploadRepairDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        RemComp<StationAiUploadHackedComponent>(uid);
        _appearance.SetData(uid, StationAiUploadConsoleVisuals.Compromised, false);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} reparou o console de upload de leis {ToPrettyString(uid):target}, removendo o hack da IA Malf.");

        _popup.PopupEntity(Loc.GetString("station-ai-upload-console-repaired"), uid, args.User, PopupType.Medium);
    }
}

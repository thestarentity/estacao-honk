using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Wires;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    // Cor avermelhada que denuncia a sabotagem — só fica visível com o painel de manutenção aberto.
    private static readonly Color ConsoleCompromisedTint = new Color(1f, 0.3f, 0.3f);

    private void InitializeUploadConsole()
    {
        SubscribeLocalEvent<SiliconLawUpdaterComponent, GetStationAiRadialEvent>(OnUploadConsoleGetRadial);
        SubscribeLocalEvent<SiliconLawUpdaterComponent, AppearanceChangeEvent>(OnUploadConsoleAppearanceChange);
    }

    /// <summary>
    /// Aplica (ou remove) o tell visual de console comprometido pela IA Malf.
    /// O tell só aparece quando o console está comprometido
    /// (<see cref="StationAiUploadConsoleVisuals.Compromised"/>) E o painel de manutenção
    /// está aberto (<see cref="WiresVisuals.MaintenancePanelState"/>): de painel fechado o
    /// console parece intacto, obrigando a tripulação a investigar com a chave de fenda.
    /// </summary>
    private void OnUploadConsoleAppearanceChange(Entity<SiliconLawUpdaterComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        _appearance.TryGetData<bool>(ent.Owner, StationAiUploadConsoleVisuals.Compromised, out var compromised, args.Component);
        _appearance.TryGetData<bool>(ent.Owner, WiresVisuals.MaintenancePanelState, out var panelOpen, args.Component);

        var color = compromised && panelOpen ? ConsoleCompromisedTint : Color.White;

        // LayerSetColor(string key) ignora silenciosamente se a camada não existir — seguro.
        _sprite.LayerSetColor((ent.Owner, args.Sprite), "computerLayerScreen", color);
    }

    private void OnUploadConsoleGetRadial(Entity<SiliconLawUpdaterComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Só IA Malf pode hackear o console.
        if (!LocalAiIsHostile())
            return;

        // Se o console já foi hackeado, não mostra o botão de novo.
        if (HasComp<StationAiUploadHackedComponent>(ent.Owner))
            return;

        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "generalhack"),
            Tooltip = Loc.GetString("station-ai-radial-hack-upload-console"),
            Event = new StationAiHackUploadConsoleEvent(),
        });
    }
}

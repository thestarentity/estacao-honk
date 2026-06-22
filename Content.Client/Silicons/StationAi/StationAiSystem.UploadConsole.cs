using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    // Cor discretamente avermelhada para indicar que o console de upload foi comprometido.
    private static readonly Color ConsoleCompromisedTint = new Color(1f, 0.3f, 0.3f);

    private void InitializeUploadConsole()
    {
        SubscribeLocalEvent<SiliconLawUpdaterComponent, GetStationAiRadialEvent>(OnUploadConsoleGetRadial);
        SubscribeLocalEvent<SiliconLawUpdaterComponent, AppearanceChangeEvent>(OnUploadConsoleAppearanceChange);
    }

    /// <summary>
    /// Aplica (ou remove) o tell visual de console comprometido pela IA Malf.
    /// Quando <see cref="StationAiUploadConsoleVisuals.Compromised"/> for verdadeiro,
    /// tinge a camada <c>computerLayerScreen</c> de vermelho discreto; caso contrário,
    /// restaura a cor padrão (branco).
    /// </summary>
    private void OnUploadConsoleAppearanceChange(Entity<SiliconLawUpdaterComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        _appearance.TryGetData<bool>(ent.Owner, StationAiUploadConsoleVisuals.Compromised, out var compromised, args.Component);

        var color = compromised ? ConsoleCompromisedTint : Color.White;

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

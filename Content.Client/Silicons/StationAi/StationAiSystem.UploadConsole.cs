using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private void InitializeUploadConsole()
    {
        SubscribeLocalEvent<SiliconLawUpdaterComponent, GetStationAiRadialEvent>(OnUploadConsoleGetRadial);
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

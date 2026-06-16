using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private void InitializeApc()
    {
        SubscribeLocalEvent<StationAiApcControllableComponent, GetStationAiRadialEvent>(OnApcGetRadial);
    }

    private void OnApcGetRadial(Entity<StationAiApcControllableComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Sob lei hostil e ainda não hackeada: a única opção é Hackear.
        if (LocalAiIsHostile() && !ent.Comp.Hacked)
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "hackapc"),
                Tooltip = Loc.GetString("ai-apc-hack"),
                Event = new StationAiApcHackEvent(),
            });
            return;
        }

        // Hackeada (ou IA leal, que nunca hackeia): cortar/restaurar energia.
        var powerOn = ent.Comp.PowerOn;
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, powerOn ? "turn_on" : "turn_off"),
            Tooltip = Loc.GetString(powerOn ? "ai-apc-power-off" : "ai-apc-power-on"),
            Event = new StationAiApcToggleEvent(),
        });
    }
}

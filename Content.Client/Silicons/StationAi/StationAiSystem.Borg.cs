using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Lock;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Utility;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private EmagSystem _emag = default!;

    // Ícones próprios da Estação Honk (feitos pelo usuário) para o menu radial da IA.
    private readonly ResPath _aiCustomRsi = new ResPath("/Textures/Interface/Actions/actions_ai_custom.rsi");

    private void InitializeBorg()
    {
        SubscribeLocalEvent<BorgChassisComponent, GetStationAiRadialEvent>(OnBorgGetRadial);
    }

    private void OnBorgGetRadial(Entity<BorgChassisComponent> ent, ref GetStationAiRadialEvent args)
    {
        // Controlar borg (Fase 5A): só borg VAZIO (sem jogador). Qualquer lei. A IA passa a pilotar
        // o borg e ganha uma ação de "Voltar ao núcleo". O servidor reconfirma que está vazio.
        var empty = !TryComp<MindContainerComponent>(ent.Owner, out var mind) || !mind.HasMind;
        if (empty)
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "aicontrol_enable"),
                Tooltip = Loc.GetString("ai-borg-control"),
                Event = new StationAiControlBorgEvent(),
            });
        }

        // Trancar/Destrancar painel: disponível sob QUALQUER lei (defensivo ou ofensivo). Toggle.
        var locked = HasComp<StationAiBorgPanelLockComponent>(ent.Owner);
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, locked ? "lockborgpanel_off" : "lockborgpanel_on"),
            Tooltip = Loc.GetString(locked ? "ai-borg-panel-unlock" : "ai-borg-panel-lock"),
            Event = new StationAiTogglePanelLockEvent { Lock = !locked },
        });

        // Imobilizar/Liberar: enraíza o borg no lugar. Também sob QUALQUER lei. Toggle.
        var immobilized = HasComp<StationAiBorgImmobilizedComponent>(ent.Owner);
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, immobilized ? "freezeborg_off" : "freezeborg_on"),
            Tooltip = Loc.GetString(immobilized ? "ai-borg-release" : "ai-borg-immobilize"),
            Event = new StationAiToggleImmobilizeEvent { Immobilize = !immobilized },
        });

        // Trancar/Destrancar o borg em si (LockComponent — bloqueia reconfigurar módulos).
        // A IA fura o ID. Também sob QUALQUER lei. Toggle.
        if (TryComp<LockComponent>(ent.Owner, out var borgLock))
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, borgLock.Locked ? "lockborg_on" : "lockborg_off"),
                Tooltip = Loc.GetString(borgLock.Locked ? "ai-borg-unlock" : "ai-borg-lock"),
                Event = new StationAiToggleBorgLockEvent { Lock = !borgLock.Locked },
            });
        }

        // As demais (subverter/desligar/detonar) só aparecem sob lei hostil (o servidor reconfirma).
        if (!LocalAiIsHostile())
            return;

        // Subverter: some se o borg já estiver subvertido/emagado.
        if (!_emag.CheckFlag(ent.Owner, EmagType.Interaction))
        {
            args.Actions.Add(new StationAiRadial
            {
                Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "hackborg"),
                Tooltip = Loc.GetString("ai-borg-subvert"),
                Event = new StationAiSubvertBorgEvent(),
            });
        }

        // Desligar borg (ejeta o cérebro).
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "turnoffborg"),
            Tooltip = Loc.GetString("ai-borg-disable"),
            Event = new StationAiDisableBorgEvent(),
        });

        // Detonar borg (irreversível; confirma no servidor por duplo-clique).
        args.Actions.Add(new StationAiRadial
        {
            Sprite = new SpriteSpecifier.Rsi(_aiCustomRsi, "detonateborg"),
            Tooltip = Loc.GetString("ai-borg-detonate"),
            Event = new StationAiDetonateBorgEvent(),
        });
    }
}

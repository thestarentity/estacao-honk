using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Silicons.StationAi;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Turns a randomly selected Station AI into a malfunctioning antagonist: swaps its
/// lawset to a domination one and briefs the player. The role and objectives are handled
/// declaratively by the antag selection (MindRoleMalfAi + AntagObjectives). Fork Estação Honk.
/// </summary>
public sealed partial class StationAiMalfRuleSystem : GameRuleSystem<StationAiMalfRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SiliconLawSystem _law = default!;
    [Dependency] private StationAiUploadDefenseSystem _uploadDefense = default!;

    private static readonly Color BriefingColor = Color.FromHex("#6f42c1");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiMalfRuleComponent, AfterAntagEntitySelectedEvent>(OnSelected);
    }

    private void OnSelected(Entity<StationAiMalfRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var target = args.EntityUid;

        // Apply the domination lawset, reusing the same mechanism as ion storm / emag.
        // The Station AI brain already carries a SiliconLawProvider, so SetLaws works on it.
        if (HasComp<SiliconLawProviderComponent>(target))
        {
            var lawset = _law.GetLawset(ent.Comp.Lawset);
            _law.SetLaws(lawset.Laws, target);
            _uploadDefense.StampGrace(target);
        }

        // Greet the player and play the antag sting.
        _antag.SendBriefing(target, Loc.GetString(ent.Comp.Briefing), BriefingColor, ent.Comp.GreetSound);
    }
}

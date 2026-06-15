using Content.Server.GameTicking.Rules;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Game rule that turns a randomly selected Station AI into a malfunctioning antagonist.
/// Applies a domination lawset, the malf role and a briefing. Fork Estação Honk.
/// </summary>
[RegisterComponent, Access(typeof(StationAiMalfRuleSystem))]
public sealed partial class StationAiMalfRuleComponent : Component
{
    /// <summary>
    /// The lawset applied to the AI when it becomes malfunctioning.
    /// </summary>
    [DataField]
    public ProtoId<SiliconLawsetPrototype> Lawset = "MalfAi";

    /// <summary>
    /// The briefing locale id shown to the malfunctioning AI.
    /// </summary>
    [DataField]
    public LocId Briefing = "malf-ai-role-greeting";

    /// <summary>
    /// Sound played when the AI is briefed. Uses the silicon law-corruption sting (fork Estação Honk)
    /// instead of the default syndicate one, to match the "malfunctioning AI" theme.
    /// </summary>
    [DataField]
    public SoundSpecifier? GreetSound = new SoundPathSpecifier("/Audio/Ambience/Antag/silicon_lawboard_antimov.ogg");
}

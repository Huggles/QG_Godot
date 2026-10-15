using System;

/// <summary>
/// The entry point of an expansion's code assembly. <see cref="ExpansionLoader"/> creates one instance of
/// every non-abstract implementation it finds in the assembly and calls <see cref="Register"/> once, at
/// boot.
///
/// Deliberately small: it grows one hook at a time, as the engine gains something an expansion has to
/// switch on. Core reads what the hooks contribute; it never references an expansion's own types, which
/// is what lets an expansion ship as a separate assembly at all.
/// </summary>
public interface IExpansionRules
{
    /// <summary>The manifest id of the expansion this belongs to, e.g. "total_war".</summary>
    string Id { get; }

    /// <summary>Contribute this expansion's types to the engine. Runs before any game data is loaded.</summary>
    void Register(ExpansionRegistrar registrar);

    /// <summary>Add this expansion's rules to a session it is enabled for. Runs on every peer, before any state is built.</summary>
    void Configure(RuleSet rules) { }
}

/// <summary>What an expansion may add to the engine at boot. Handed to <see cref="IExpansionRules.Register"/>.</summary>
public sealed class ExpansionRegistrar
{
    private readonly string _expansionId;

    internal ExpansionRegistrar(string expansionId)
    {
        _expansionId = expansionId;
    }

    /// <summary>
    /// Adds a replicated message the expansion defines itself: its DTO joins the polymorphic wire format
    /// under <paramref name="discriminator"/>, and <paramref name="fromDto"/> rebuilds the message on a
    /// receiving peer — the two things a core message gets from a [JsonDerivedType] line on
    /// GameMessageDto and an arm in GameMessage.FromDto.
    ///
    /// Prefix the discriminator with the expansion (e.g. "TW.") so it can never collide with a core one.
    /// </summary>
    public void AddMessage<TDto>(string discriminator, Func<TDto, GameMessage> fromDto) where TDto : GameMessageDto
    {
        GameMessageJson.RegisterExpansionMessage(_expansionId, typeof(TDto), discriminator, dto => fromDto((TDto)dto));
    }
}

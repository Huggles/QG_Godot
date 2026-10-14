using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// The serializer settings for anything that carries a <see cref="GameMessageDto"/>: the live message
/// stream, the rejoin buffer and the event log inside a save.
///
/// Core messages register on <see cref="GameMessageDto"/> with [JsonDerivedType] attributes, which can only
/// name types this assembly can see. An expansion's own messages live in its own assembly, so they are
/// added at runtime instead: <see cref="ExpansionRegistrar.AddMessage{TDto}"/> records them here, and a
/// resolver modifier appends them to GameMessageDto's polymorphism options. With no expansion messages
/// registered the modifier adds nothing, so the base wire format is byte-for-byte what the default
/// options produced before.
///
/// Every registration must happen before the first message is (de)serialized: System.Text.Json freezes
/// options on first use. <see cref="ExpansionLoader"/> runs at boot, ahead of any session, so it does.
/// </summary>
public static class GameMessageJson
{
    private sealed record ExpansionMessage(string ExpansionId, Type DtoType, string Discriminator, Func<GameMessageDto, GameMessage> FromDto);

    private static readonly List<ExpansionMessage> _expansionMessages = new();

    private static JsonSerializerOptions _options;
    private static JsonSerializerOptions _indentedOptions;

    /// <summary>The live stream and the rejoin buffer.</summary>
    public static JsonSerializerOptions Options => _options ??= Build(writeIndented: false);

    /// <summary>
    /// Saves. Indented on purpose — see <see cref="SaveGameService"/>. Reading ignores indentation, so a
    /// save is read with these same options.
    /// </summary>
    public static JsonSerializerOptions IndentedOptions => _indentedOptions ??= Build(writeIndented: true);

    /// <summary>The DTO types and discriminators <paramref name="expansionId"/> registered.</summary>
    public static List<(Type DtoType, string Discriminator)> RegisteredMessages(string expansionId)
        => _expansionMessages.Where(m => m.ExpansionId == expansionId).Select(m => (m.DtoType, m.Discriminator)).ToList();

    /// <summary>The message <paramref name="dto"/> encodes, if an expansion registered its DTO type.</summary>
    public static bool TryCreateExpansionMessage(GameMessageDto dto, out GameMessage message)
    {
        ExpansionMessage registration = _expansionMessages.Find(m => m.DtoType == dto.GetType());
        message = registration?.FromDto(dto);
        return message != null;
    }

    internal static void RegisterExpansionMessage(string expansionId, Type dtoType, string discriminator, Func<GameMessageDto, GameMessage> fromDto)
    {
        if (_options != null || _indentedOptions != null)
            throw new InvalidOperationException(
                $"Expansion '{expansionId}' registered message '{discriminator}' after the first message was serialized");

        if (_expansionMessages.Any(m => m.Discriminator == discriminator || m.DtoType == dtoType)
            || CoreDiscriminators().Contains(discriminator))
            throw new InvalidOperationException(
                $"Expansion '{expansionId}' registered message '{discriminator}' ({dtoType.Name}), which is already taken");

        _expansionMessages.Add(new ExpansionMessage(expansionId, dtoType, discriminator, fromDto));
    }

    private static JsonSerializerOptions Build(bool writeIndented) => new()
    {
        WriteIndented = writeIndented,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddExpansionMessages } },
    };

    private static void AddExpansionMessages(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(GameMessageDto) || typeInfo.PolymorphismOptions == null) return;

        foreach (ExpansionMessage message in _expansionMessages)
            typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(message.DtoType, message.Discriminator));
    }

    private static IEnumerable<string> CoreDiscriminators() =>
        typeof(GameMessageDto)
            .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute), inherit: false)
            .Cast<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
            .Select(attribute => attribute.TypeDiscriminator as string);
}

/// <summary>
/// What a <see cref="TargetRef"/> refers to. Deliberately mirrors <c>CliOptionKind</c> in
/// <c>scripts/C#/Cli/InputRequestSpec.cs</c>, which solves the same problem — describing a set of
/// selectable things of mixed kinds, each identified by an int id — for the CLI.
///
/// Only <see cref="Country"/> and <see cref="Unit"/> name a place on the board, so only those two are
/// drawn by the hover preview — and separately, since a country glows where a unit puts up its own
/// marker. Card and Faction targets are still worth declaring: they say truthfully what an action
/// reaches, and a future presentation can highlight them without this type changing shape.
/// </summary>
public enum TargetKind
{
    Country,
    Unit,
    Card,
    Faction,
}

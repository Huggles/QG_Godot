/// <summary>
/// One thing an action could affect: what kind of thing, and which one.
///
/// A struct rather than a class because a <see cref="TargetSet"/> holds many of them and they are
/// pure value identity — two refs to the same country are the same ref. The record gives structural
/// equality, which is what lets <see cref="TargetSet"/> deduplicate with a plain HashSet.
/// </summary>
public readonly record struct TargetRef(TargetKind Kind, int Id);

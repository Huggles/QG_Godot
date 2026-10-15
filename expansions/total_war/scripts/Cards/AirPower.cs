using System.Collections.Generic;

/// <summary>
/// An Air Power card. It has no effect of its own: it is the card the Air step discards to deploy an Air
/// Force or to use Air Superiority (see AirStep), and like any card it may be discarded to satisfy the Play step.
/// </summary>
public partial class AirPower : CardLogic
{
    public override List<CardStep> OnActivate() => new();
}

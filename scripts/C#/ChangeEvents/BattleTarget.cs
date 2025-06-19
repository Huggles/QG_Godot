public class BattleTarget
{

    public int Id { get; set; }
    public TargetType Type { get; set; }

    public BattleTarget(int id, TargetType type)
    {
        this.Id = id;
        this.Type = type;
    }

    public BattleCountryChangeEvent ToAttackChangeEvent(Faction faction)
    {
        if (Type == TargetType.UNIT)
        {
            return new BattleUnitChangeEvent(faction, Id);
        }
        else
        {
            return new BattleCountryChangeEvent(faction, Id);
        }
    }
    public override bool Equals(object obj)
    {
        BattleTarget other = obj as BattleTarget;
        return other.Id == Id && other.Type == Type;
    }
    public override int GetHashCode()
    {
        return this.Id.GetHashCode() + this.Type.GetHashCode();
    }
}


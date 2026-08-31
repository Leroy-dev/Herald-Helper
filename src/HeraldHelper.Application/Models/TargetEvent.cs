namespace HeraldHelper.Application.Models;

public enum TargetMembership
{
    Unknown,
    Member,
    NonMember
}

public sealed record TargetEvent(
    string Name,
    TargetMembership Membership,
    int OccurrenceOrdinal = 1)
{
    public TargetEvent(string Name, bool IsMemberTarget, int OccurrenceOrdinal = 1)
        : this(Name, IsMemberTarget ? TargetMembership.Member : TargetMembership.NonMember, OccurrenceOrdinal)
    {
    }

    public bool IsMemberTarget => Membership == TargetMembership.Member;
}

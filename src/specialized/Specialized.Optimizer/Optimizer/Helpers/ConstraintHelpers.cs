using Specialized.Optimizer.Models.Enums;
using Specialized.Optimizer.Optimizer.Models.Domain;

namespace Specialized.Optimizer.Optimizer.Helpers;

internal static class ConstraintHelpers
{
    public static Score CalculateConstraintScore(this PlanningDomain domain)
    {
        var hcScore =
            domain.HC1_TotalConstraint //hc1
            + domain.HC2_RequiredTasksMustBeScheduledConstraint //hc2
            + 0 //hc3
            + 0 // hc4
            + 0 //hc5
            + domain.HC6_RespectWeekMinOptCountConstraint
            + domain.HC7_TotalConstraint
            + 0 //hc8
            + 0; //hc9

        var scScore = 
            -1 * 100 * domain.SC1_TotalPriorityConstraint
            + 150 * domain.SC2_TotalConstraint
            + domain.SC3_TotalConstraint * (domain.Domain.DifficultTaskSchedulingStrategy == DifficultTaskSchedulingStrategy.Even ? -1 : 1)
            + -1 * domain.SC4_TotalConstraint
            + 50 * domain.SC5_MinimizeDifferenceFromWeekOptConstraint
            + 50 * domain.SC6_TotalConstraint
            + domain.SC7_TotalDifficultyDifference;

        return new(hcScore, scScore);
    }

    public readonly record struct Score(int Hard, int Soft) : IComparable<Score>
    {
        public int CompareTo(Score other)
        {
            if (Hard < other.Hard || (Hard == other.Hard && Soft < other.Soft)) return -1;
            if (Hard == other.Hard && Soft == other.Soft) return 0;
            return 1;
        }

        public static bool operator <(Score a, Score b) => a.CompareTo(b) < 0;
        public static bool operator >(Score a, Score b) => a.CompareTo(b) > 0;
        public static bool operator <=(Score a, Score b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Score a, Score b) => a.CompareTo(b) >= 0;
    }
}

using Specialized.Optimizer.Optimizer.Models.Domain;
using Specialized.Optimizer.Optimizer.Moves;

namespace Specialized.Optimizer.Optimizer;

//LAHC (Late Acceptance Hill Climbing) stage for the optimizer
internal class LAHCStage(MoveEngine moveEngine)
{
    public PlanningDomain Run(PlanningDomain domain)
    {
        return domain;
    }
}

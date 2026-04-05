using Specialized.Optimizer.Optimizer.Models.Domain;
using static Specialized.Optimizer.Optimizer.Moves.MoveEngine;

namespace Specialized.Optimizer.Optimizer.Moves;

internal class MoveSelector
{
    public MoveSelector(Random? random = null, MoveEngine? moveEngine = null)
    {
        _random = random ?? new Random();
        _moveEngine = moveEngine ?? new MoveEngine(_random);
    }

    private readonly Random _random;
    private readonly MoveEngine _moveEngine;

    public PlanningDomain MakeMove(PlanningDomain domain, bool createSnapshot = true)
    {
        var randomDouble = _random.NextDouble();

        if (randomDouble <= 0.01)
        {
            var scope = _random.NextDouble() switch
            {
                0.4 => MoveScope.Operational,
                0.3 + (0.4) => MoveScope.Tactical,
                0.2 + (0.3 + 0.4) => MoveScope.SemiStrategic,
                _ => MoveScope.Strategic //0.1
            };
            var recreateStrategy = _random.NextDouble() switch
            {
                0.2 => RecreateStrategy.None,
                0.5 + (0.2) => RecreateStrategy.ConstructionHeuristics,
                _ => RecreateStrategy.ConsecutiveAdd //0.3
            };

            domain = _moveEngine.RuinRecreate(domain, scope, recreateStrategy, createSnapshot: createSnapshot);

            //make random moves
            if(recreateStrategy == RecreateStrategy.None)
            {
                var totalActualFreeMinutes = (int)domain.PlanningDays.SelectMany(pd => pd.ActualTimeWindows)
                .Sum(atw => (atw.End - atw.Start).TotalMinutes);
                totalActualFreeMinutes /= 30;

                for(var i = 0; i < totalActualFreeMinutes; i++)
                    domain = MakeMove(domain, createSnapshot: false);
            }

            return domain;
        }
        else if (randomDouble < 0.1 + (0.01))
        {
            return _moveEngine.AddTask(domain, createSnapshot: createSnapshot);
        }
        else if (randomDouble < 0.1 + (0.1 + 0.01))
        {
            return _moveEngine.RemoveTask(domain, createSnapshot: createSnapshot);
        }
        else if (randomDouble < 0.35 + (0.1 + 0.1 + 0.01))
        {
            var scope = _random.NextDouble() switch
            {
                0.75 => MoveScope.Tactical,
                _ => MoveScope.Strategic
            };
            return _moveEngine.SwapTasks(domain, scope, createSnapshot: createSnapshot);
        }
        else //0.44
        {
            var maxCascadeSequence = _random.NextDouble() switch
            {
                0.35 => 1,
                0.3 + (0.35) => 2,
                0.2 + (0.3 + 0.35) => 3,
                0.1 + (0.2 + 0.3 + 0.35) => 4,
                _ => 5 //0.05
            };
            var scope = _random.NextDouble() switch
            {
                0.75 => MoveScope.Tactical,
                _ => MoveScope.Strategic
            };

            return _moveEngine.MoveTasks(domain, maxCascadeSequence, scope, createSnapshot: createSnapshot);
        }
    }
}

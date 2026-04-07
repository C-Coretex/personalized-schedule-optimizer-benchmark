using Specialized.Optimizer.Optimizer.Models.Domain;
using static Specialized.Optimizer.Optimizer.Moves.MoveEngine;

namespace Specialized.Optimizer.Optimizer.Moves;

internal class MoveSelector
{
    public MoveSelector(Random? random = null, MoveEngine? moveEngine = null)
    {
        _random = random ?? new Random();
        _moveEngine = moveEngine ?? new MoveEngine(_random);
        _lahcEngine = new(this, optimizationIterations: 1_000, ruinEnabled: false);
    }

    private readonly Random _random;
    private readonly MoveEngine _moveEngine;
    private readonly LAHCEngine _lahcEngine;
    public int LAHCIterations { get; private set; }

    public PlanningDomain MakeMove(PlanningDomain domain, bool includeRuinRecreate = true, bool createSnapshot = true)
        => MakeMove(domain, out _, includeRuinRecreate, createSnapshot);

    public PlanningDomain MakeMove(PlanningDomain domain, out MoveType moveTypeSelected, bool includeRuinRecreate = true, bool createSnapshot = true)
    {
        var randomDouble = _random.NextDouble();

        var ruinRecreateChance = includeRuinRecreate ? 0.01 : 0;
        if (randomDouble <= ruinRecreateChance)
        {
            moveTypeSelected = MoveType.RuinRecreate;
            var scope = _random.NextDouble() switch
            {
                < 0.4 => MoveScope.Operational,
                < 0.3 + (0.4) => MoveScope.Tactical,
                < 0.2 + (0.3 + 0.4) => MoveScope.SemiStrategic,
                _ => MoveScope.Strategic //0.1
            };

            var previousAvailableItems = domain.AvailableTasksPool.Values.Sum();
            domain = _moveEngine.RuinRecreate(domain, scope, createSnapshot: createSnapshot);
            var newAvailableItems = domain.AvailableTasksPool.Values.Sum();

            var difference = Math.Abs(newAvailableItems - previousAvailableItems);
            if (difference == 0)
                return domain;

            //now run short LAHC to construct something SA can accept
            //TODO: can be done in parallel to not stop actual SA run, since this could take a while because of large amount of LAHC iterations
            var lahcIterations = Math.Min(10_000, difference * 100);
            domain = _lahcEngine.Run(domain, optimizationIterations: lahcIterations);
            LAHCIterations += lahcIterations;

            return domain;
        }
        else if (randomDouble < 0.1 + ruinRecreateChance)
        {
            moveTypeSelected = MoveType.Add;
            return _moveEngine.AddTask(domain, createSnapshot: createSnapshot);
        }
        else if (randomDouble < 0.1 + (0.1 + ruinRecreateChance))
        {
            moveTypeSelected = MoveType.Remove;
            return _moveEngine.RemoveTask(domain, createSnapshot: createSnapshot);
        }
        else if (randomDouble < 0.35 + (0.1 + 0.1 + ruinRecreateChance))
        {
            moveTypeSelected = MoveType.Swap;
            var scope = _random.NextDouble() switch
            {
                < 0.75 => MoveScope.Tactical,
                _ => MoveScope.Strategic
            };
            return _moveEngine.SwapTasks(domain, scope, createSnapshot: createSnapshot);
        }
        else //0.44
        {
            moveTypeSelected = MoveType.CascadeMove;
            var maxCascadeSequence = _random.NextDouble() switch
            {
                < 0.35 => 1,
                < 0.3 + (0.35) => 2,
                < 0.2 + (0.3 + 0.35) => 3,
                < 0.1 + (0.2 + 0.3 + 0.35) => 4,
                _ => 5 //0.05
            };
            var scope = _random.NextDouble() switch
            {
                < 0.75 => MoveScope.Tactical,
                _ => MoveScope.Strategic
            };

            return _moveEngine.MoveTasks(domain, maxCascadeSequence, scope, createSnapshot: createSnapshot);
        }
    }
}

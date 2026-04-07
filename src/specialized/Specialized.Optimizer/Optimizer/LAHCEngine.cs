using Specialized.Optimizer.Optimizer.Models.Domain;
using Specialized.Optimizer.Optimizer.Moves;
using static Specialized.Optimizer.Optimizer.Helpers.ConstraintHelpers;

namespace Specialized.Optimizer.Optimizer;

//LAHC (Late Acceptance Hill Climbing) stage for the optimizer
internal class LAHCEngine
{
    private readonly MoveSelector _moveSelector;
    private readonly int _bufferSize;
    private readonly bool _ruinEnabled;
    private readonly int _optimizationIterations;

    public LAHCEngine(MoveSelector moveSelector, int optimizationIterations, int? bufferSize = null, bool ruinEnabled = true)
    {
        _moveSelector = moveSelector;

        _optimizationIterations = optimizationIterations;
        _bufferSize = bufferSize ?? optimizationIterations / 30;
        _ruinEnabled = ruinEnabled;
    }
    public PlanningDomain Run(PlanningDomain domain, int? optimizationIterations = null)
    {
        //smaller buffer size for more greedy search for local optima (since we have only few iterations to recreate after ruin)
        var bufferSize = optimizationIterations is not null ? optimizationIterations.Value / 30 : _bufferSize;
        optimizationIterations ??= _optimizationIterations;
        var current = domain;
        var currentScore = current.CalculateConstraintScore();

        var history = new Score[bufferSize];
        for(var i = 0; i < history.Length; i++)
            history[i] = currentScore;

        for (var step = 0; step < optimizationIterations; step++)
        {
            var candidate = _moveSelector.MakeMove(current, includeRuinRecreate: _ruinEnabled);
            var candidateScore = candidate.CalculateConstraintScore();

            var i = step % bufferSize;
            if (candidateScore < history[i % bufferSize])
            {
                current = candidate;
                currentScore = candidateScore;
            }

            history[i] = currentScore;
        }

        return current;
    }
}

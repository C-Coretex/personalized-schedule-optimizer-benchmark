using System.Diagnostics;
using Specialized.Optimizer.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;
using Specialized.Optimizer.Optimizer.Moves;

namespace Specialized.Optimizer.Optimizer;

//SA (Simulated Annealing) stage for the optimizer
internal class SAEngine
{
    private readonly MoveSelector _moveSelector;
    private readonly Random _random;
    private readonly int _initialHard;
    private readonly int _initialSoft;
    private readonly int _optimizationTimeInSeconds;
    private readonly int _optimizationTimeInMilliseconds;

    public SAEngine(MoveSelector moveSelector, Random random, int optimizationTimeInSeconds = 15)
    {
        _random = random;
        _moveSelector = moveSelector;

        _initialHard = 2;
        _initialSoft = 1_000;
        _optimizationTimeInSeconds = optimizationTimeInSeconds;
        _optimizationTimeInMilliseconds = _optimizationTimeInSeconds * 1000;
    }

    public PlanningDomain Run(PlanningDomain domain, bool continueIfBetterMoveFound = false)
    {
        var bestDomain = domain;
        var bestDomainScore = bestDomain.CalculateConstraintScore();
        var currentDomain = bestDomain;
        var currentDomainScore = currentDomain.CalculateConstraintScore();
        var iteration = 0;

        var sw = new Stopwatch();
        sw.Start();
        //var temperature = (hard: 0, soft: 100);

        var previouslyLoggedTimeMs = long.MinValue;
        var bestMoveFoundTimeInMs = long.MinValue;
        //for(var i = 0; i < 3_000_000; ++i)
        while(sw.ElapsedMilliseconds < _optimizationTimeInMilliseconds || (continueIfBetterMoveFound && sw.ElapsedMilliseconds - bestMoveFoundTimeInMs < 1_000))
        {
            domain = _moveSelector.MakeMove(currentDomain, 1 - ((double)sw.ElapsedMilliseconds / _optimizationTimeInMilliseconds), out var selectedMove);
            var domainScore = domain.CalculateConstraintScore();
            if (domainScore < bestDomainScore)
            {
                bestDomain = domain;
                bestDomainScore = domainScore;
                bestMoveFoundTimeInMs = sw.ElapsedMilliseconds; //we can also check how much score is better
                //Console.WriteLine($"Move selected: {selectedMove}");
            }

            var temperature = GetTemperature(1 - ((double)sw.ElapsedMilliseconds / _optimizationTimeInMilliseconds));
            var randomValue = _random.NextDouble();
            var expHard = temperature.hard > 0 ? Math.Exp(-(domainScore.Hard - currentDomainScore.Hard) / (double)temperature.hard) : 0;
            var expSoft = temperature.soft > 0 ? Math.Exp(-(domainScore.Soft - currentDomainScore.Soft) / (double)temperature.soft) : 0;
            if (domainScore < currentDomainScore 
                || ((domainScore.Hard > currentDomainScore.Hard && randomValue < expHard) 
                || (domainScore.Hard <= currentDomainScore.Hard && randomValue < expSoft)))
            {
                currentDomain = domain;
                currentDomainScore = domainScore;
            }

            iteration++;
            //Replace with logger.Debug
            if (sw.ElapsedMilliseconds > previouslyLoggedTimeMs)
            {
                Console.WriteLine($"Total iterations {iteration + _moveSelector.LAHCIterations} (SA {iteration}/ LAHC {_moveSelector.LAHCIterations}), best score: {bestDomainScore}, time elapsed: {sw.Elapsed.TotalSeconds:F1}s");
                previouslyLoggedTimeMs = sw.ElapsedMilliseconds + 1000;
            }
        }

        Console.WriteLine($"Total iterations {iteration + _moveSelector.LAHCIterations} (SA {iteration}/ LAHC {_moveSelector.LAHCIterations}), best solution: {bestDomainScore}");

        return bestDomain;
    }

    private (int hard, int soft) GetTemperature(double timePercentLeft)
    {
        const double hardPhaseEnd = 0.7;
        const double softFloor = 0.5;
        const double softCurve = 2.0; // exponent: 2 = quadratic, 3 = cubic (steeper early drop)

        if (timePercentLeft > hardPhaseEnd)
        {
            double hardProgress = (timePercentLeft - hardPhaseEnd) / (1.0 - hardPhaseEnd);
            double softProgress = softFloor + (1.0 - softFloor) * hardProgress;
            return ((int)(_initialHard * hardProgress), (int)(_initialSoft * softProgress));
        }
        else
        {
            double linearProgress = timePercentLeft / hardPhaseEnd; // 1.0 → 0.0
            double softProgress = Math.Pow(linearProgress, softCurve);
            return (0, (int)(_initialSoft * softProgress));
        }
    }
}

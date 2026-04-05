using Specialized.Optimizer.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;
using Specialized.Optimizer.Optimizer.Moves;
using System.Diagnostics;

namespace Specialized.Optimizer.Optimizer;

//SA (Simulated Annealing) stage for the optimizer
internal class SAStage
{
    private readonly MoveSelector _moveSelector;
    private readonly Random _random;
    private readonly int _initialHard;
    private readonly int _initialSoft;
    private readonly int _optimizationTimeInSeconds;
    private readonly int _optimizationTimeInMilliseconds;

    public SAStage(MoveSelector moveSelector, Random random)
    {
        _random = random;
        _moveSelector = moveSelector;

        _initialHard = 0;
        _initialSoft = 1_000;
        _optimizationTimeInSeconds = 10;
        _optimizationTimeInMilliseconds = _optimizationTimeInSeconds * 1000;
    }

    public PlanningDomain Run(PlanningDomain domain)
    {
        var bestDomain = domain;
        var bestDomainScore = bestDomain.CalculateConstraintScore();
        var currentDomain = bestDomain;
        var currentDomainScore = currentDomain.CalculateConstraintScore();
        var iteration = 0;

        var sw = new Stopwatch();
        sw.Start();
        var temperature = (hard: 0, soft: 100);

        //while(sw.ElapsedMilliseconds < _optimizationTimeInMilliseconds)
        for(var i = 0; i < 1_000_000; ++i)
        {
            domain = _moveSelector.MakeMove(currentDomain);
            var domainScore = domain.CalculateConstraintScore();
            if (domainScore < bestDomainScore)
            {
                bestDomain = domain;
                bestDomainScore = domainScore;
            }

           // var temperature = GetTemperature(1 - ((double)sw.ElapsedMilliseconds / _optimizationTimeInMilliseconds));
            var randomValue = _random.NextDouble();
            var expHard = temperature.hard > 0 ? Math.Exp((domainScore.Hard - currentDomainScore.Hard) / temperature.hard) : 0;
            var expSoft = temperature.soft > 0 ? Math.Exp((domainScore.Soft - currentDomainScore.Soft) / temperature.soft) : 0;
            if (domainScore < currentDomainScore 
                || ((domainScore.Hard > currentDomainScore.Hard && randomValue < expHard) 
                || (domainScore.Hard <= currentDomainScore.Hard && randomValue < expSoft)))
            {
                currentDomain = domain;
                currentDomainScore = domainScore;
            }

            //Replace with logger.Debug
            if (++iteration % 10_000 == 0)
                Console.WriteLine($"Iteration {iteration}, best score: {bestDomainScore}, time elapsed: {sw.Elapsed.TotalSeconds:F1}s");
        }

        Console.WriteLine($"Total iterations: {iteration}, best solution: {bestDomainScore}");

        return bestDomain;
    }

    private (int hard, int soft) GetTemperature(double timePercentLeft)
    {
        const double hardPhaseEnd = 0.7; // hard gone by 70% remaining = first 30% of time
        const double softFloor = 0.3;    // soft doesn't go below 30% of initial during phase 1

        if (timePercentLeft > hardPhaseEnd)
        {
            double hardProgress = Math.Pow((timePercentLeft - hardPhaseEnd) / (1.0 - hardPhaseEnd), 2);
            //we use second square, so Soft decreases a lot slower at start
            double softProgress = softFloor + (1.0 - softFloor) * Math.Pow(hardProgress, 2); 
            return ((int)(_initialHard * hardProgress), (int)(_initialSoft * softProgress));
        }
        else
        {
            double softProgress = Math.Pow(timePercentLeft / hardPhaseEnd, 2);
            return (0, (int)(_initialSoft * softProgress));
        }
    }
}

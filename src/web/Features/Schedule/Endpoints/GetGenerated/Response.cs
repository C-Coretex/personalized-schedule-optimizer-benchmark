using Web.Features.Schedule.Models.Schedule;
using Web.Providers.Models;

namespace Web.Features.Schedule.Endpoints.GetGenerated
{
    public record Response(List<ResponseEntry> data);

    public record ResponseEntry(ResponseScore? score, ScheduleJobMetadata scheduleJobMetadata);

    public record ResponseScore(JobScore score, List<ResponseConstraintScore> hardConstraintScores, List<ResponseConstraintScore> softConstraintScores)
    {
        public static ResponseScore FromConstraintValues(ResponseConstraintScore hc1, ResponseConstraintScore hc2, 
            ResponseConstraintScore hc3, ResponseConstraintScore hc4, ResponseConstraintScore hc5, ResponseConstraintScore hc6, 
            ResponseConstraintScore hc7, ResponseConstraintScore hc8, ResponseConstraintScore sc1, ResponseConstraintScore sc2, 
            ResponseConstraintScore sc3, ResponseConstraintScore sc4, ResponseConstraintScore sc5, ResponseConstraintScore sc6, 
            ResponseConstraintScore sc7)
        {
            var hcScore = hc1.score + hc2.score + (int)Math.Ceiling(hc3.score/60d) + (int)Math.Ceiling(hc4.score / 60d)
                + hc5.score + hc6.score + hc7.score + (int)Math.Ceiling(hc8.score / 60d);
            var scScore = 1000 * sc1.score + 1250 * sc2.score + sc3.score + sc4.score + 50 * sc5.score + 50 * sc6.score + sc7.score;

            return new ResponseScore(
                new(hcScore, scScore),
                [ hc1, hc2, hc3, hc4, hc5, hc6, hc7, hc8 ], 
                [ sc1, sc2, sc3, sc4, sc5, sc6, sc7 ]
            );
        }
    }

    public record ResponseConstraintScore(string constraintName, int score);
}

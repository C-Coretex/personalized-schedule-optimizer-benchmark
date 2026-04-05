using Web.Features.Schedule.Models.Schedule;
using Web.Providers.Models;
using Web.Providers.Schedule.Models.Tasks;

namespace Web.Features.Schedule.Endpoints.GetGenerated
{
    public record Response(List<ResponseEntry> Data);

    public record ResponseEntry(ResponseScore? Score, ScheduleJobMetadata ScheduleJobMetadata, List<DynamicTask> UnscheduledDynamicTasks);

    public record ResponseScore(JobScore Score, List<ResponseConstraintScore> HardConstraintScores, List<ResponseConstraintScore> SoftConstraintScores)
    {
        public static ResponseScore FromConstraintValues(ResponseConstraintScore hc1, ResponseConstraintScore hc2, 
            ResponseConstraintScore hc3, ResponseConstraintScore hc4, ResponseConstraintScore hc5, ResponseConstraintScore hc6, 
            ResponseConstraintScore hc7, ResponseConstraintScore hc8, ResponseConstraintScore hc9, ResponseConstraintScore sc1, 
            ResponseConstraintScore sc2, ResponseConstraintScore sc3, ResponseConstraintScore sc4, ResponseConstraintScore sc5, 
            ResponseConstraintScore sc6, ResponseConstraintScore sc7)
        {
            var hcScore = hc1.Score + hc2.Score + (int)Math.Ceiling(hc3.Score/60d) + (int)Math.Ceiling(hc4.Score / 60d)
                + hc5.Score + hc6.Score + hc7.Score + (int)Math.Ceiling(hc8.Score / 60d) + hc9.Score;
            var scScore = 100 * sc1.Score + 500 * sc2.Score + sc3.Score + sc4.Score + 50 * sc5.Score + 50 * sc6.Score + sc7.Score;

            return new ResponseScore(
                new(hcScore, scScore),
                [ hc1, hc2, hc3, hc4, hc5, hc6, hc7, hc8 ], 
                [ sc1, sc2, sc3, sc4, sc5, sc6, sc7 ]
            );
        }
    }

    public record ResponseConstraintScore(string ConstraintName, int Score);
}

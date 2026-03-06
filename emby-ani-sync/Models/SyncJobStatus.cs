using System;

namespace emby_ani_sync.Models
{
    public static class SyncJobState
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    public class SyncJobStatus
    {
        public string JobId { get; set; }
        public string State { get; set; }
        public string Phase { get; set; }
        public string Message { get; set; }
        public int? Current { get; set; }
        public int? Total { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public string Error { get; set; }

        public SyncJobStatus Clone()
        {
            return new SyncJobStatus
            {
                JobId = JobId,
                State = State,
                Phase = Phase,
                Message = Message,
                Current = Current,
                Total = Total,
                StartedAtUtc = StartedAtUtc,
                FinishedAtUtc = FinishedAtUtc,
                Error = Error,
            };
        }
    }
}

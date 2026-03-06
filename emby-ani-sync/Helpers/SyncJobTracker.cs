using System;
using System.Collections.Generic;
using System.Linq;
using emby_ani_sync.Models;
using Microsoft.Extensions.Logging;

namespace emby_ani_sync.Helpers
{
    public class SyncJobTracker
    {
        private readonly Dictionary<string, SyncJobStatus> _jobs = new Dictionary<string, SyncJobStatus>();
        private readonly object _lock = new object();
        private readonly TimeSpan _retentionPeriod;
        private readonly ILogger<SyncJobTracker> _logger;

        public SyncJobTracker(ILoggerFactory loggerFactory, TimeSpan? retentionPeriod = null)
        {
            _retentionPeriod = retentionPeriod ?? TimeSpan.FromMinutes(30);
            _logger = loggerFactory.CreateLogger<SyncJobTracker>();
        }

        public SyncJobStatus CreateJob(string phase, string message)
        {
            lock (_lock)
            {
                CleanupExpiredJobs();

                SyncJobStatus job = new SyncJobStatus
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    State = SyncJobState.Queued,
                    Phase = phase,
                    Message = message,
                    StartedAtUtc = DateTime.UtcNow,
                };

                _jobs[job.JobId] = job;
                return job.Clone();
            }
        }

        public bool TryGetJob(string jobId, out SyncJobStatus jobStatus)
        {
            lock (_lock)
            {
                CleanupExpiredJobs();
                if (_jobs.TryGetValue(jobId, out SyncJobStatus existing))
                {
                    jobStatus = existing.Clone();
                    return true;
                }

                jobStatus = null;
                return false;
            }
        }

        public SyncJobStatus MarkRunning(
            string jobId,
            string phase,
            string message,
            int? current = null,
            int? total = null
        )
        {
            return Update(
                jobId,
                job =>
                {
                    job.State = SyncJobState.Running;
                    job.Phase = phase;
                    job.Message = message;
                    job.Current = current;
                    job.Total = total;
                    job.Error = null;
                }
            );
        }

        public SyncJobStatus MarkCompleted(string jobId, string message = null)
        {
            return Update(
                jobId,
                job =>
                {
                    job.State = SyncJobState.Completed;
                    job.FinishedAtUtc = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        job.Message = message;
                    }
                    else if (string.IsNullOrWhiteSpace(job.Message))
                    {
                        job.Message = "Sync completed.";
                    }

                    job.Error = null;
                }
            );
        }

        public SyncJobStatus MarkFailed(string jobId, string error, string message = null)
        {
            return Update(
                jobId,
                job =>
                {
                    job.State = SyncJobState.Failed;
                    job.FinishedAtUtc = DateTime.UtcNow;
                    job.Error = error;
                    job.Message = string.IsNullOrWhiteSpace(message) ? "Sync failed." : message;
                }
            );
        }

        private SyncJobStatus Update(string jobId, Action<SyncJobStatus> updateAction)
        {
            lock (_lock)
            {
                CleanupExpiredJobs();

                if (!_jobs.TryGetValue(jobId, out SyncJobStatus job))
                {
                    _logger.LogWarning("Attempted to update missing sync job {JobId}", jobId);
                    throw new KeyNotFoundException($"No sync job exists with an id of {jobId}.");
                }

                updateAction(job);
                return job.Clone();
            }
        }

        private void CleanupExpiredJobs()
        {
            List<string> expired = _jobs
                .Where(item =>
                    item.Value.FinishedAtUtc.HasValue
                    && item.Value.FinishedAtUtc.Value.Add(_retentionPeriod) <= DateTime.UtcNow
                )
                .Select(item => item.Key)
                .ToList();

            foreach (string jobId in expired)
            {
                _jobs.Remove(jobId);
            }
        }
    }
}

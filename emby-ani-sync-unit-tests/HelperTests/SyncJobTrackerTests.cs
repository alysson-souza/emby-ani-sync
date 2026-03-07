using System;
using System.Threading.Tasks;
using emby_ani_sync.Helpers;
using emby_ani_sync.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace emby_ani_sync_unit_tests.HelperTests;

public class SyncJobTrackerTests
{
    [Test]
    public void CreateAndUpdateJobTracksManualSyncState()
    {
        SyncJobTracker tracker = new SyncJobTracker(new NullLoggerFactory());

        SyncJobStatus job = tracker.CreateJob("Queued", "Waiting to start.");
        Assert.That(job.State, Is.EqualTo(SyncJobState.Queued));
        Assert.That(job.Phase, Is.EqualTo("Queued"));
        Assert.That(job.Message, Is.EqualTo("Waiting to start."));

        SyncJobStatus running = tracker.MarkRunning(job.JobId, "Running", "Syncing anime.", 2, 5);
        Assert.That(running.State, Is.EqualTo(SyncJobState.Running));
        Assert.That(running.Current, Is.EqualTo(2));
        Assert.That(running.Total, Is.EqualTo(5));

        Assert.That(tracker.TryGetJob(job.JobId, out SyncJobStatus storedRunning), Is.True);
        Assert.That(storedRunning.State, Is.EqualTo(SyncJobState.Running));
        Assert.That(storedRunning.Message, Is.EqualTo("Syncing anime."));

        SyncJobStatus completed = tracker.MarkCompleted(job.JobId, "Done.");
        Assert.That(completed.State, Is.EqualTo(SyncJobState.Completed));
        Assert.That(completed.Message, Is.EqualTo("Done."));
        Assert.That(completed.FinishedAtUtc, Is.Not.Null);
    }

    [Test]
    public async Task CompletedJobsExpireAfterRetentionWindow()
    {
        SyncJobTracker tracker = new SyncJobTracker(new NullLoggerFactory(), TimeSpan.Zero);
        SyncJobStatus job = tracker.CreateJob("Queued", "Waiting to start.");
        tracker.MarkCompleted(job.JobId, "Finished.");

        await Task.Delay(20);

        Assert.That(tracker.TryGetJob(job.JobId, out _), Is.False);
    }
}

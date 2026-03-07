var AniSyncManualSyncState = {
  pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47',
  sessionKey: 'aniSync.manualSyncJob',
};

define([], function () {
  return function (view) {
    view.addEventListener('viewshow', function () {
      var generalFunctionsUrl = ApiClient.getUrl('web/ConfigurationPage', { name: 'AniSync_CommonJs' });
      require([generalFunctionsUrl], function (common) {
        initializeManualSyncPage(view, common);
      });
    });

    view.addEventListener('viewhide', function () {
      var pageState = view.__aniSyncManualSyncState;
      if (pageState && pageState.poller) {
        pageState.poller.stop();
      }
    });
  };
});

function initializeManualSyncPage(view, common) {
  var pageState = view.__aniSyncManualSyncState;
  if (!pageState) {
    pageState = {
      common: common,
      poller: null,
      activeJobId: null,
      initialized: false,
    };
    view.__aniSyncManualSyncState = pageState;
  }

  pageState.common = common;
  common.setTabs(common.TabManualSync, common.getTabs);

  if (!pageState.initialized) {
    bindManualSyncEvents(view, pageState);
    pageState.initialized = true;
  }

  loadManualSyncParameters(view, pageState)
    .then(function () {
      updateManualSyncActionState(view);
      resumeManualSyncJob(view, pageState);
    })
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#manualSyncPageNotice'), 'error', message);
      });
    });
}

function bindManualSyncEvents(view, pageState) {
  view.querySelector('#run').addEventListener('click', function () {
    runManualSync(view, pageState);
  });

  view.querySelector('#selectAction').addEventListener('change', function () {
    updateManualSyncActionState(view);
  });
}

function loadManualSyncParameters(view, pageState) {
  var common = pageState.common;
  var selectedProvider = view.querySelector('#selectSyncProvider').value;
  var selectedUser = view.querySelector('#selectSyncUser').value;
  var parametersUrl = ApiClient.getUrl(
    '/AniSync/parameters' + buildQueryPrefix(common.buildIncludesQuery([common.parameterInclude.ProviderList])),
  );

  return Promise.all([common.requestJson({ url: parametersUrl }), ApiClient.getUsers()]).then(function (results) {
    var parameters = results[0] || {};
    var users = results[1] || [];
    common.setProviderSelection(view, parameters.providerList || [], '#selectSyncProvider', {
      selectedValue: selectedProvider,
    });
    common.populateUserList(view, users, '#selectSyncUser', selectedUser || (users[0] && users[0].Id));
  });
}

function buildQueryPrefix(query) {
  return query ? '?' + query : '';
}

function updateManualSyncActionState(view) {
  var action = view.querySelector('#selectAction').value;
  var disableProviderFields = action === 'UpdateProvider';

  view.querySelector('#selectSyncProvider').disabled = disableProviderFields;
  view.querySelector('#status').disabled = disableProviderFields;
  view.querySelector('#actionDescription').textContent = disableProviderFields
    ? 'Push watched progress from Emby to every provider this user has connected.'
    : 'Pull the selected provider progress into Emby for one user.';
  view.querySelector('#providerDescription').textContent = disableProviderFields
    ? 'Not needed when updating providers from Emby; Ani-Sync will use every connected provider for this user.'
    : 'Choose which provider should be used as the source of truth.';
  view.querySelector('#statusDescription').textContent = disableProviderFields
    ? 'Not used for provider updates because the watched progress comes from Emby.'
    : 'Filter which provider entries should be imported into Emby.';
}

function runManualSync(view, pageState) {
  var common = pageState.common;
  var runButton = view.querySelector('#run');
  var action = view.querySelector('#selectAction').value;
  var jobUrl = ApiClient.getUrl(
    '/AniSync/sync?provider=' +
      encodeURIComponent(view.querySelector('#selectSyncProvider').value) +
      '&userId=' +
      encodeURIComponent(view.querySelector('#selectSyncUser').value) +
      '&status=' +
      encodeURIComponent(view.querySelector('#status').value) +
      '&syncAction=' +
      encodeURIComponent(action),
  );

  common.clearNotice(view.querySelector('#manualSyncPageNotice'));
  common.setButtonBusy(runButton, true, 'Starting...');
  renderManualSyncStatus(view, {
    State: 'queued',
    Phase: 'Starting sync',
    Message: 'Submitting the manual sync request.',
    Current: null,
    Total: null,
    StartedAtUtc: new Date().toISOString(),
  });

  common
    .requestJson({ type: 'POST', url: jobUrl })
    .then(function (jobStatus) {
      pageState.activeJobId = jobStatus.JobId;
      common.setSessionValue(AniSyncManualSyncState.sessionKey, { jobId: jobStatus.JobId });
      renderManualSyncStatus(view, jobStatus);
      startManualSyncPolling(view, pageState);
    })
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#manualSyncPageNotice'), 'error', message);
        common.setButtonBusy(runButton, false);
        hideManualSyncStatus(view);
      });
    });
}

function resumeManualSyncJob(view, pageState) {
  var common = pageState.common;
  var storedJob = common.getSessionValue(AniSyncManualSyncState.sessionKey);
  if (!storedJob || !storedJob.jobId) {
    return;
  }

  pageState.activeJobId = storedJob.jobId;
  fetchManualSyncStatus(view, pageState, true)
    .then(function (jobStatus) {
      if (!jobStatus) {
        return;
      }

      if (isJobTerminal(jobStatus)) {
        finishManualSyncPolling(view, pageState, jobStatus);
      } else {
        startManualSyncPolling(view, pageState);
      }
    })
    .catch(function () {
      common.removeSessionValue(AniSyncManualSyncState.sessionKey);
    });
}

function startManualSyncPolling(view, pageState) {
  var common = pageState.common;
  var runButton = view.querySelector('#run');

  if (pageState.poller) {
    pageState.poller.stop();
  }

  common.setButtonBusy(runButton, true, 'Running...');
  pageState.poller = common.createPoller(function () {
    return fetchManualSyncStatus(view, pageState, false)
      .then(function (jobStatus) {
        if (!jobStatus) {
          return;
        }

        if (isJobTerminal(jobStatus)) {
          finishManualSyncPolling(view, pageState, jobStatus);
        }
      })
      .catch(function (error) {
        if (pageState.poller) {
          pageState.poller.stop();
        }
        common.getErrorText(error).then(function (message) {
          common.setNotice(
            view.querySelector('#manualSyncPageNotice'),
            'warning',
            'Unable to refresh job status: ' + message,
          );
          common.setButtonBusy(runButton, false);
        });
      });
  }, 2000);
  pageState.poller.start();
}

function finishManualSyncPolling(view, pageState, jobStatus) {
  var common = pageState.common;
  if (pageState.poller) {
    pageState.poller.stop();
    pageState.poller = null;
  }

  renderManualSyncStatus(view, jobStatus);
  common.setButtonBusy(view.querySelector('#run'), false);

  if (jobStatus.State === 'failed') {
    common.setNotice(
      view.querySelector('#manualSyncPageNotice'),
      'error',
      jobStatus.Error || jobStatus.Message || 'Manual sync failed.',
    );
  } else {
    common.setNotice(
      view.querySelector('#manualSyncPageNotice'),
      'success',
      jobStatus.Message || 'Manual sync finished.',
    );
  }
}

function fetchManualSyncStatus(view, pageState, silent) {
  if (!pageState.activeJobId) {
    return Promise.resolve(null);
  }

  var common = pageState.common;
  var statusUrl = ApiClient.getUrl('/AniSync/sync/status?jobId=' + encodeURIComponent(pageState.activeJobId));
  return common
    .requestJson({ url: statusUrl })
    .then(function (jobStatus) {
      renderManualSyncStatus(view, jobStatus);
      return jobStatus;
    })
    .catch(function (error) {
      if (!silent) {
        throw error;
      }
      return null;
    });
}

function isJobTerminal(jobStatus) {
  return jobStatus && (jobStatus.State === 'completed' || jobStatus.State === 'failed');
}

function hideManualSyncStatus(view) {
  view.querySelector('#manualSyncStatusPanel').style.display = 'none';
}

function renderManualSyncStatus(view, jobStatus) {
  var common = view.__aniSyncManualSyncState.common;
  var panel = view.querySelector('#manualSyncStatusPanel');
  panel.style.display = 'block';

  var stateText = (jobStatus.State || 'queued').toLowerCase();
  var tone = 'info';
  if (stateText === 'completed') {
    tone = 'success';
  } else if (stateText === 'failed') {
    tone = 'error';
  } else if (stateText === 'queued') {
    tone = 'warning';
  }

  common.setStatusPill(view.querySelector('#manualSyncStatusPill'), tone, toTitleCase(stateText));
  view.querySelector('#manualSyncPhase').textContent = jobStatus.Phase || 'Working';
  view.querySelector('#manualSyncMessage').textContent = jobStatus.Message || '';
  view.querySelector('#manualSyncStatusMeta').textContent = formatJobMeta(jobStatus);

  var progressText = 'Waiting for detailed progress...';
  var progressWidth = 12;
  if (typeof jobStatus.Current === 'number' && typeof jobStatus.Total === 'number' && jobStatus.Total > 0) {
    progressText = jobStatus.Current + ' of ' + jobStatus.Total + ' completed';
    progressWidth = Math.max(12, Math.min(100, Math.round((jobStatus.Current / jobStatus.Total) * 100)));
  } else if (stateText === 'completed') {
    progressText = 'Completed';
    progressWidth = 100;
  } else if (stateText === 'failed') {
    progressText = 'Stopped with an error';
    progressWidth = 100;
  }

  view.querySelector('#manualSyncProgressText').textContent = progressText;
  view.querySelector('#manualSyncProgressBar').style.width = progressWidth + '%';
}

function formatJobMeta(jobStatus) {
  var parts = [];
  if (jobStatus.JobId) {
    parts.push('Job ' + jobStatus.JobId.substring(0, 8));
  }

  if (jobStatus.StartedAtUtc) {
    parts.push('Started ' + new Date(jobStatus.StartedAtUtc).toLocaleTimeString());
  }

  if (jobStatus.FinishedAtUtc) {
    parts.push('Finished ' + new Date(jobStatus.FinishedAtUtc).toLocaleTimeString());
  }

  return parts.join(' • ');
}

function toTitleCase(value) {
  if (!value) {
    return '';
  }

  return value.charAt(0).toUpperCase() + value.slice(1);
}

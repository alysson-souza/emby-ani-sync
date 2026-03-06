var AniSyncAdminPage = {
  pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47',
  pendingAuthKey: 'aniSync.admin.pendingAuth',
};

define([], function () {
  return function (view) {
    view.addEventListener('viewshow', function () {
      var generalFunctionsUrl = ApiClient.getUrl('web/ConfigurationPage', { name: 'AniSync_CommonJs' });
      require([generalFunctionsUrl], function (common) {
        initializeAdminPage(view, common);
      });
    });
  };
});

function initializeAdminPage(view, common) {
  var pageState = view.__aniSyncAdminPageState;
  if (!pageState) {
    pageState = {
      common: common,
      config: null,
      parameters: null,
      users: [],
      initialized: false,
      localApiUrl: '',
    };
    view.__aniSyncAdminPageState = pageState;
  }

  pageState.common = common;
  pageState.pendingAuth = common.getSessionValue(AniSyncAdminPage.pendingAuthKey);
  common.setTabs(common.TabGeneral, common.getTabs);

  if (!pageState.initialized) {
    bindAdminPageEvents(view, pageState);
    pageState.initialized = true;
  }

  refreshAdminData(view, pageState).then(function () {
    maybeResumePendingAuth(view, pageState);
  });
}

function bindAdminPageEvents(view, pageState) {
  var refreshSelectors = [
    '#apiUrl',
    '#callbackRedirectUrlInput',
    '#armServerBaseUrlInput',
    '#animeListSaveLocation',
    '#clientId',
    '#clientSecret',
    '#shikimoriAppName',
    '#simklUpdateAll',
    '#enableUserPages',
    '#watchedTickboxUpdatesProvider',
    '#UpdateNsfw',
    '#linkTimeExpire',
    '#userClientId',
    '#userClientSecret',
    '#PlanToWatchOnly',
    '#RewatchCompleted',
  ];

  for (var index = 0; index < refreshSelectors.length; index++) {
    attachRefreshListener(view, refreshSelectors[index], pageState);
  }

  view.querySelector('#selectProvider').addEventListener('change', function () {
    renderAdminPage(view, pageState);
  });

  view.querySelector('#selectUser').addEventListener('change', function () {
    renderAdminPage(view, pageState);
  });

  view.querySelector('#TemplateConfigForm').addEventListener('submit', function (event) {
    event.preventDefault();
    saveAdminConfiguration(view, pageState, {
      button: view.querySelector('#saveConfigButton'),
      busyText: 'Saving...',
      showSuccessNotice: true,
      processResult: true,
    });
  });

  view.querySelector('#copyCallbackUrlButton').addEventListener('click', function () {
    copyCallbackPreview(view, pageState);
  });

  view.querySelector('#testAnimeListSaveLocation').addEventListener('click', function () {
    runTestAnimeListSaveLocation(view, pageState);
  });

  view.querySelector('#authorizeDevice').addEventListener('click', function () {
    connectSelectedUser(view, pageState);
  });

  view.querySelector('#testAuthentication').addEventListener('click', function () {
    testSelectedUserAuthentication(view, pageState, {});
  });

  view.querySelector('#deauthenticate').addEventListener('click', function () {
    deauthenticateSelectedUser(view, pageState);
  });

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'visible') {
      maybeResumePendingAuth(view, pageState);
    }
  });

  window.addEventListener('focus', function () {
    maybeResumePendingAuth(view, pageState);
  });
}

function attachRefreshListener(view, selector, pageState) {
  var element = view.querySelector(selector);
  if (!element) {
    return;
  }

  var eventName = element.type === 'checkbox' || element.tagName === 'SELECT' ? 'change' : 'input';
  element.addEventListener(eventName, function () {
    refreshDerivedState(view, pageState);
  });
}

function refreshAdminData(view, pageState) {
  var common = pageState.common;
  var currentProvider = getSelectedProvider(view);
  var currentUserId = getSelectedUserId(view);

  Dashboard.showLoadingMsg();
  return Promise.all([
    common.requestJson({ url: ApiClient.getUrl('/AniSync/parameters') }),
    ApiClient.getPluginConfiguration(AniSyncAdminPage.pluginUniqueId),
    ApiClient.getUsers(),
  ])
    .then(function (results) {
      pageState.parameters = results[0] || {};
      pageState.config = normalizePluginConfiguration(results[1] || {});
      pageState.users = results[2] || [];
      pageState.localApiUrl = buildLocalApiUrl(pageState.parameters);

      var providerList = pageState.parameters.providerList || [];
      var selectedUserId = currentUserId || getDefaultUserId(pageState.users);
      var selectedProvider = currentProvider || getDefaultProvider(providerList);
      var connectedProviders = getConnectedProviderSet(getUserConfiguration(pageState.config, selectedUserId, false));

      common.populateUserList(view, pageState.users, '#selectUser', selectedUserId);
      common.setProviderSelection(view, providerList, '#selectProvider', {
        connectedProviders: connectedProviders,
        selectedValue: selectedProvider,
      });

      renderAdminPage(view, pageState);
    })
    .catch(function (error) {
      return common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#saveStatusNotice'), 'error', message);
      });
    })
    .finally(function () {
      Dashboard.hideLoadingMsg();
    });
}

function normalizePluginConfiguration(config) {
  config.UserConfig = config.UserConfig || [];
  config.ProviderApiAuth = config.ProviderApiAuth || [];

  for (var index = 0; index < config.UserConfig.length; index++) {
    var userConfig = config.UserConfig[index];
    userConfig.UserApiAuth = userConfig.UserApiAuth || [];
    userConfig.LibraryToCheck = userConfig.LibraryToCheck || [];
    userConfig.KeyPairs = userConfig.KeyPairs || [];
    if (userConfig.PlanToWatchOnly === undefined) {
      userConfig.PlanToWatchOnly = true;
    }
    if (userConfig.RewatchCompleted === undefined) {
      userConfig.RewatchCompleted = true;
    }
  }

  return config;
}

function buildLocalApiUrl(parameters) {
  var https = parameters && parameters.https;
  var localIpAddress = parameters && parameters.localIpAddress ? parameters.localIpAddress : 'localhost';
  var localPort = parameters && parameters.localPort ? parameters.localPort : '';
  return (https ? 'https://' : 'http://') + localIpAddress + (localPort ? ':' + localPort : '');
}

function renderAdminPage(view, pageState) {
  if (!pageState.config || !pageState.parameters) {
    return;
  }

  populateServerFields(view, pageState);
  populateAdvancedFields(view, pageState);
  populateProviderFields(view, pageState);
  populateUserFields(view, pageState);
  refreshDerivedState(view, pageState);
}

function populateServerFields(view, pageState) {
  var config = pageState.config;
  view.querySelector('#apiUrl').value = config.callbackUrl || '';
  view.querySelector('#callbackRedirectUrlInput').value = config.callbackRedirectUrl || '';
  view.querySelector('#armServerBaseUrlInput').value = config.armServerBaseUrl || '';
  view.querySelector('#animeListSaveLocation').value = config.animeListSaveLocation || '';
  view.querySelector('#localApiUrl').textContent = 'Detected local URL: ' + pageState.localApiUrl;
  view.querySelector('#userAddress').textContent = 'Current dashboard URL: ' + ApiClient.serverAddress();
  view.querySelector('#callbackRedirectUrlDescription').innerHTML =
    'Optional. Supports <code>{{LocalIpAddress}}</code> and <code>{{LocalPort}}</code>.';
  updateCallbackPreview(view, pageState);
}

function populateAdvancedFields(view, pageState) {
  var config = pageState.config;
  view.querySelector('#enableUserPages').checked = !!config.enableUserPages;
  view.querySelector('#watchedTickboxUpdatesProvider').checked = !!config.watchedTickboxUpdatesProvider;
  view.querySelector('#UpdateNsfw').checked = !!config.updateNsfw;
  view.querySelector('#linkTimeExpire').value =
    config.authenticationLinkExpireTimeMinutes && config.authenticationLinkExpireTimeMinutes !== 0
      ? config.authenticationLinkExpireTimeMinutes
      : 1440;
  view.querySelector('#shikimoriAppName').value = config.shikimoriAppName || '';
  view.querySelector('#simklUpdateAll').checked = !!config.simklUpdateAll;
}

function populateProviderFields(view, pageState) {
  var provider = getSelectedProvider(view);
  var providerAuth = getProviderApiAuth(pageState.config, provider);
  if (providerAuth) {
    view.querySelector('#clientId').value = providerAuth.ClientId || '';
    view.querySelector('#clientSecret').value = providerAuth.ClientSecret || '';
  } else {
    view.querySelector('#clientId').value = '';
    view.querySelector('#clientSecret').value = '';
  }

  clearFieldErrors(view);
}

function populateUserFields(view, pageState) {
  var userConfig = getSelectedUserConfiguration(view, pageState, false);
  var effectiveConfig = userConfig || createDefaultUserConfiguration(getSelectedUserId(view));

  view.querySelector('#PlanToWatchOnly').checked = effectiveConfig.PlanToWatchOnly;
  view.querySelector('#RewatchCompleted').checked = effectiveConfig.RewatchCompleted;
  pageState.common.renderLibraryCheckboxes(
    view.querySelector('#libraries'),
    pageState.parameters.libraries || [],
    effectiveConfig.LibraryToCheck,
    'No libraries were found on this server.',
  );

  var selectedProvider = getSelectedProvider(view);
  if (selectedProvider === 'Annict') {
    var annictAuth = getUserApiAuth(effectiveConfig, 'Annict');
    view.querySelector('#userClientId').value = annictAuth && annictAuth.AccessToken ? annictAuth.AccessToken : '';
    view.querySelector('#userClientSecret').value = '';
  } else {
    view.querySelector('#userClientId').value = '';
    view.querySelector('#userClientSecret').value = '';
  }
}

function refreshDerivedState(view, pageState) {
  clearFieldErrors(view);
  updateProviderSelectLabels(view, pageState);
  updateCallbackPreview(view, pageState);
  updateWorkflowSummary(view, pageState);
  updateProviderCardState(view, pageState);
  updateConnectionCardState(view, pageState);
}

function updateProviderSelectLabels(view, pageState) {
  var selectedProvider = getSelectedProvider(view);
  pageState.common.setProviderSelection(view, pageState.parameters.providerList || [], '#selectProvider', {
    connectedProviders: getConnectedProviderSet(getSelectedUserConfiguration(view, pageState, false)),
    selectedValue: selectedProvider,
  });
}

function updateCallbackPreview(view, pageState) {
  var apiUrl = view.querySelector('#apiUrl').value || pageState.localApiUrl || ApiClient.serverAddress();
  apiUrl = apiUrl.replace(/\/+$/, '');
  view.querySelector('#generalCallbackUrlInput').value = apiUrl + '/emby/AniSync/authCallback';
}

function updateProviderCardState(view, pageState) {
  var common = pageState.common;
  var provider = getSelectedProvider(view);
  var definition = getProviderDefinition(provider);
  var providerConfigured = isProviderConfigured(view, definition);
  var providerStatusText = definition.requiresProviderCredentials
    ? providerConfigured
      ? 'App credentials saved'
      : 'App credentials needed'
    : 'Per-user sign-in';
  var providerStatusTone = definition.requiresProviderCredentials
    ? providerConfigured
      ? 'success'
      : 'warning'
    : 'info';

  common.setStatusPill(view.querySelector('#providerConfigStatus'), providerStatusTone, providerStatusText);
  setOptionalText(view.querySelector('#providerSetupHint'), definition.providerHint);
  view.querySelector('#clientIdLabel').textContent = definition.providerClientIdLabel || 'Client ID';
  setOptionalText(view.querySelector('#clientIdDescription'), definition.providerClientIdDescription);
  view.querySelector('#clientSecretLabel').textContent = definition.providerClientSecretLabel || 'Client Secret';
  setOptionalHtml(view.querySelector('#clientSecretDescription'), definition.providerClientSecretDescription);

  toggleElement(view.querySelector('#providerCredentialSection'), definition.requiresProviderCredentials);
  toggleElement(view.querySelector('#providerOauthSection'), !!definition.showProviderOAuth);
  toggleElement(
    view.querySelector('#clientSecretContainer'),
    definition.requiresProviderCredentials && !!definition.showProviderSecret,
  );
  toggleElement(view.querySelector('#shikimoriAppNameContainer'), !!definition.showShikimoriAppName);
  toggleElement(view.querySelector('#simklUpdateAllContainer'), !!definition.showSimklOptions);

  if (!definition.requiresProviderCredentials) {
    common.clearNotice(view.querySelector('#providerCardNotice'));
  }
}

function updateConnectionCardState(view, pageState) {
  var common = pageState.common;
  var provider = getSelectedProvider(view);
  var definition = getProviderDefinition(provider);
  var userConfig = getSelectedUserConfiguration(view, pageState, false);
  var userAuth = getUserApiAuth(userConfig, provider);
  var isConnected = !!userAuth;
  var isPending = hasPendingAuthForCurrentSelection(view, pageState);
  var connectionPillText = isConnected ? 'Connected' : isPending ? 'Waiting for authorization' : 'Not connected';
  var connectionPillTone = isConnected ? 'success' : isPending ? 'warning' : 'warning';
  var connectionText = isConnected ? buildConnectionSummary(provider, userAuth) : definition.disconnectedSummary;

  common.setStatusPill(view.querySelector('#connectionStatusPill'), connectionPillTone, connectionPillText);
  view.querySelector('#connectionStatusText').textContent = connectionText;
  setOptionalText(
    view.querySelector('#connectionHelpText'),
    isPending
      ? 'Finish approval in the opened tab, then return here. Ani-Sync will retest automatically.'
      : definition.connectionHelp,
  );

  view.querySelector('#authorizeDevice span').textContent = definition.connectButtonText;
  toggleElement(view.querySelector('#userCredentialSection'), !!definition.usesUserCredentials);
  toggleElement(view.querySelector('#userClientSecretContainer'), !!definition.showUserSecret);
  view.querySelector('#userClientIdLabel').textContent = definition.userClientIdLabel || 'Username';
  setOptionalText(view.querySelector('#userClientIdDescription'), definition.userClientIdDescription);
  view.querySelector('#userClientSecretLabel').textContent = definition.userClientSecretLabel || 'Password';
  setOptionalText(view.querySelector('#userClientSecretDescription'), definition.userClientSecretDescription);
  view.querySelector('#testAuthentication').disabled = !isConnected && !isPending;
  view.querySelector('#deauthenticate').disabled = !isConnected && !isPending;

  if (isPending) {
    common.setNotice(
      view.querySelector('#connectionCardNotice'),
      'info',
      'Authorization is in progress. Return to this tab after approving access so Ani-Sync can verify the connection.',
    );
  } else if (!isConnected) {
    common.clearNotice(view.querySelector('#connectionCardNotice'));
  }

  if (!isPending) {
    hideAuthorizeLink(view);
  }
}

function updateWorkflowSummary(view, pageState) {
  var common = pageState.common;
  var provider = getSelectedProvider(view);
  var providerName = getProviderDisplayName(pageState, provider);
  var userName = getUserDisplayName(pageState, getSelectedUserId(view));
  var definition = getProviderDefinition(provider);
  var userConfig =
    getSelectedUserConfiguration(view, pageState, false) || createDefaultUserConfiguration(getSelectedUserId(view));
  var userAuth = getUserApiAuth(userConfig, provider);
  var providerReady = isProviderConfigured(view, definition);
  var userConnected = !!userAuth;
  var hasPending = hasPendingAuthForCurrentSelection(view, pageState);
  var selectedLibraries = getSelectedLibraryIds(view);
  var workflowTone = 'warning';
  var workflowText = 'Needs setup';
  var workflowSummary = '';
  var stepOneTitle;
  var stepOneText;
  var stepTwoTitle;
  var stepTwoText;
  var stepThreeTitle;
  var stepThreeText;

  if (hasPending) {
    workflowSummary =
      'Finish approving ' +
      providerName +
      ' for ' +
      userName +
      ', then return here so Ani-Sync can verify the connection.';
    workflowText = 'Waiting for approval';
  } else if (definition.requiresProviderCredentials && !providerReady) {
    workflowSummary = providerName + ' still needs app credentials before ' + userName + ' can connect.';
    workflowText = 'Provider app setup needed';
  } else if (!userConnected) {
    workflowSummary = providerName + ' is ready. Connect ' + userName + ' next.';
    workflowText = definition.usesUserCredentials ? 'User sign-in needed' : 'User authorization needed';
  } else {
    workflowSummary =
      userName +
      ' is connected to ' +
      providerName +
      '. Review the sync scope below or switch the selectors to configure another pairing.';
    workflowTone = selectedLibraries.length === 0 ? 'info' : 'success';
    workflowText = 'Ready';
  }

  if (definition.requiresProviderCredentials) {
    stepOneTitle = providerReady ? 'Provider app is ready' : 'Save provider app credentials';
    stepOneText = providerReady
      ? providerName + ' app credentials are stored once and reused for every Emby user.'
      : 'Use Provider setup below to save the Client ID and Client Secret for ' + providerName + '.';
  } else {
    stepOneTitle = 'No provider app setup is needed';
    stepOneText = providerName + ' uses per-user credentials, so there is nothing to save in Provider setup.';
  }

  if (hasPending) {
    stepTwoTitle = 'Finish provider approval';
    stepTwoText = 'The approval page is already open for ' + userName + '. Return here after granting access.';
  } else if (userConnected) {
    stepTwoTitle = 'User is connected';
    stepTwoText = buildConnectionSummary(provider, userAuth);
  } else {
    stepTwoTitle = definition.usesUserCredentials ? 'Sign in the selected user' : 'Authorize the selected user';
    stepTwoText = definition.connectionHelp;
  }

  if (!userConnected && !hasPending) {
    stepThreeTitle = 'Save sync scope after the connection works';
    stepThreeText =
      'Once the selected user is connected, choose libraries and any user-specific sync rules, then save.';
  } else if (selectedLibraries.length === 0) {
    stepThreeTitle = 'All libraries are in scope';
    stepThreeText = 'No libraries are checked, so Ani-Sync monitors every library visible to this user.';
  } else {
    stepThreeTitle = 'Sync scope is narrowed';
    stepThreeText = selectedLibraries.length + ' libraries are selected for this user.';
  }

  common.setStatusPill(view.querySelector('#workflowStatusPill'), workflowTone, workflowText);
  view.querySelector('#workflowTitle').textContent = providerName + ' for ' + userName;
  view.querySelector('#workflowSummary').textContent = workflowSummary;
  view.querySelector('#workflowStepOneTitle').textContent = stepOneTitle;
  view.querySelector('#workflowStepOneText').textContent = stepOneText;
  view.querySelector('#workflowStepTwoTitle').textContent = stepTwoTitle;
  view.querySelector('#workflowStepTwoText').textContent = stepTwoText;
  view.querySelector('#workflowStepThreeTitle').textContent = stepThreeTitle;
  view.querySelector('#workflowStepThreeText').textContent = stepThreeText;
}

function copyCallbackPreview(view, pageState) {
  var common = pageState.common;
  var callbackUrl = view.querySelector('#generalCallbackUrlInput').value;
  common
    .copyText(callbackUrl)
    .then(function () {
      common.setNotice(view.querySelector('#providerCardNotice'), 'success', 'Callback URL copied to the clipboard.');
    })
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#providerCardNotice'), 'error', message);
      });
    });
}

function runTestAnimeListSaveLocation(view, pageState) {
  var common = pageState.common;
  var saveLocation = view.querySelector('#animeListSaveLocation').value;
  var button = view.querySelector('#testAnimeListSaveLocation');

  if (!saveLocation) {
    common.setNotice(
      view.querySelector('#serverCardNotice'),
      'warning',
      'Enter a directory to test, or leave the field blank to use the default cache location.',
    );
    return;
  }

  common.setButtonBusy(button, true, 'Testing...');
  common
    .requestText({
      url: ApiClient.getUrl('/AniSync/testAnimeListSaveLocation?saveLocation=' + encodeURIComponent(saveLocation)),
    })
    .then(function (result) {
      if (result === '') {
        common.setNotice(
          view.querySelector('#serverCardNotice'),
          'success',
          'The save location is writable. Remember to save the configuration if you want to keep it.',
        );
      } else {
        common.setNotice(view.querySelector('#serverCardNotice'), 'warning', result);
      }
    })
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#serverCardNotice'), 'error', message);
      });
    })
    .finally(function () {
      common.setButtonBusy(button, false);
    });
}

function saveAdminConfiguration(view, pageState, options) {
  options = options || {};
  var common = pageState.common;
  var busyButton = options.button || null;

  common.clearNotice(view.querySelector('#saveStatusNotice'));
  if (busyButton) {
    common.setButtonBusy(busyButton, true, options.busyText || 'Saving...');
  }

  return ApiClient.getPluginConfiguration(AniSyncAdminPage.pluginUniqueId)
    .then(function (config) {
      var normalizedConfig = normalizePluginConfiguration(config || {});
      applyFormToConfiguration(view, pageState, normalizedConfig);
      return ApiClient.updatePluginConfiguration(AniSyncAdminPage.pluginUniqueId, normalizedConfig).then(
        function (result) {
          if (options.processResult) {
            Dashboard.processPluginConfigurationUpdateResult(result);
          }
          return ApiClient.getPluginConfiguration(AniSyncAdminPage.pluginUniqueId);
        },
      );
    })
    .then(function (updatedConfig) {
      pageState.config = normalizePluginConfiguration(updatedConfig || {});
      renderAdminPage(view, pageState);
      if (options.showSuccessNotice) {
        common.setNotice(view.querySelector('#saveStatusNotice'), 'success', 'Configuration saved.');
      }
      return pageState.config;
    })
    .catch(function (error) {
      return common.getErrorText(error).then(function (message) {
        if (!options.suppressErrorNotice) {
          common.setNotice(view.querySelector('#saveStatusNotice'), 'error', message);
        }
        throw new Error(message);
      });
    })
    .finally(function () {
      if (busyButton) {
        common.setButtonBusy(busyButton, false);
      }
    });
}

function applyFormToConfiguration(view, pageState, config) {
  var provider = getSelectedProvider(view);
  var definition = getProviderDefinition(provider);
  var userId = getSelectedUserId(view);
  var userConfig = getOrCreateUserConfiguration(config, userId);

  config.animeListSaveLocation = view.querySelector('#animeListSaveLocation').value;
  config.enableUserPages = view.querySelector('#enableUserPages').checked;
  config.watchedTickboxUpdatesProvider = view.querySelector('#watchedTickboxUpdatesProvider').checked;
  config.callbackRedirectUrl = view.querySelector('#callbackRedirectUrlInput').value;
  config.shikimoriAppName = view.querySelector('#shikimoriAppName').value;
  config.simklUpdateAll = view.querySelector('#simklUpdateAll').checked;
  config.updateNsfw = view.querySelector('#UpdateNsfw').checked;
  config.armServerBaseUrl = view.querySelector('#armServerBaseUrlInput').value;

  var customApiUrl = view.querySelector('#apiUrl').value;
  if (customApiUrl) {
    config.callbackUrl = customApiUrl;
  } else {
    delete config.callbackUrl;
  }

  var linkTimeExpire = parseInt(view.querySelector('#linkTimeExpire').value, 10);
  config.authenticationLinkExpireTimeMinutes = isNaN(linkTimeExpire) || linkTimeExpire <= 0 ? 1440 : linkTimeExpire;

  userConfig.LibraryToCheck = getSelectedLibraryIds(view);
  userConfig.PlanToWatchOnly = view.querySelector('#PlanToWatchOnly').checked;
  userConfig.RewatchCompleted = view.querySelector('#RewatchCompleted').checked;
  userConfig.KeyPairs = userConfig.KeyPairs || [];
  userConfig.UserApiAuth = userConfig.UserApiAuth || [];

  if (definition.requiresProviderCredentials) {
    upsertProviderCredentials(
      config,
      provider,
      view.querySelector('#clientId').value,
      view.querySelector('#clientSecret').value,
    );
  } else {
    removeProviderCredentials(config, provider);
  }

  if (provider === 'Annict') {
    var annictToken = view.querySelector('#userClientId').value;
    if (annictToken) {
      upsertUserAuth(userConfig, 'Annict', annictToken, null);
    }
  }
}

function connectSelectedUser(view, pageState) {
  var common = pageState.common;
  var button = view.querySelector('#authorizeDevice');
  var provider = getSelectedProvider(view);
  var definition = getProviderDefinition(provider);

  common.clearNotice(view.querySelector('#providerCardNotice'));
  common.clearNotice(view.querySelector('#connectionCardNotice'));
  clearFieldErrors(view);
  common.setButtonBusy(button, true, definition.connectBusyText);

  var connectPromise;
  if (definition.requiresProviderCredentials) {
    if (!validateProviderCredentials(view, definition)) {
      common.setButtonBusy(button, false);
      return;
    }

    connectPromise = saveAdminConfiguration(view, pageState, {
      suppressErrorNotice: true,
      processResult: false,
    })
      .then(function () {
        return buildAuthorizeLink(view, pageState, provider);
      })
      .then(function (authorizeUrl) {
        showAuthorizeLink(view, authorizeUrl);
        try {
          window.open(authorizeUrl, '_blank');
        } catch (error) {
          console.warn('[AniSync] Failed to open authorization page', error);
        }
        pageState.pendingAuth = {
          provider: provider,
          userId: getSelectedUserId(view),
        };
        common.setSessionValue(AniSyncAdminPage.pendingAuthKey, pageState.pendingAuth);
        common.setNotice(
          view.querySelector('#connectionCardNotice'),
          'info',
          'Authorization opened in a new tab. After approving access, return here and Ani-Sync will verify the connection automatically.',
        );
        refreshDerivedState(view, pageState);
      });
  } else if (provider === 'Kitsu') {
    if (!validateUserCredentials(view, definition)) {
      common.setButtonBusy(button, false);
      return;
    }

    connectPromise = common
      .requestJson({
        url: ApiClient.getUrl(
          '/AniSync/passwordGrant?provider=Kitsu&userId=' +
            encodeURIComponent(getSelectedUserId(view)) +
            '&username=' +
            encodeURIComponent(view.querySelector('#userClientId').value) +
            '&password=' +
            encodeURIComponent(view.querySelector('#userClientSecret').value),
        ),
      })
      .then(function () {
        return refreshAdminData(view, pageState).then(function () {
          common.setNotice(
            view.querySelector('#connectionCardNotice'),
            'success',
            'Kitsu authentication succeeded for the selected user.',
          );
        });
      });
  } else if (provider === 'Annict') {
    if (!validateUserCredentials(view, definition)) {
      common.setButtonBusy(button, false);
      return;
    }

    connectPromise = saveAdminConfiguration(view, pageState, {
      suppressErrorNotice: true,
      processResult: false,
    }).then(function () {
      return testSelectedUserAuthentication(view, pageState, {
        suppressInfoNotice: true,
        successMessage: 'Annict token saved and verified for the selected user.',
      });
    });
  } else {
    connectPromise = Promise.resolve();
  }

  connectPromise
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#connectionCardNotice'), 'error', message);
      });
    })
    .finally(function () {
      common.setButtonBusy(button, false);
    });
}

function buildAuthorizeLink(view, pageState, provider) {
  var common = pageState.common;
  var clientId = view.querySelector('#clientId').value;
  var clientSecret = view.querySelector('#clientSecret').value;
  var apiUrl = view.querySelector('#apiUrl').value ? view.querySelector('#apiUrl').value : 'local';
  var authorizeUrl = ApiClient.getUrl(
    '/AniSync/buildAuthorizeRequestUrl?provider=' +
      encodeURIComponent(provider) +
      '&clientId=' +
      encodeURIComponent(clientId) +
      '&clientSecret=' +
      encodeURIComponent(clientSecret) +
      '&url=' +
      encodeURIComponent(apiUrl) +
      '&user=' +
      encodeURIComponent(getSelectedUserId(view)),
  );

  return common.requestText({ url: authorizeUrl });
}

function testSelectedUserAuthentication(view, pageState, options) {
  options = options || {};
  var common = pageState.common;
  var button = view.querySelector('#testAuthentication');
  var provider = getSelectedProvider(view);
  var userId = getSelectedUserId(view);

  common.setButtonBusy(button, true, 'Testing...');
  if (!options.suppressInfoNotice) {
    common.setNotice(
      view.querySelector('#connectionCardNotice'),
      'info',
      'Checking the stored provider authentication...',
    );
  }

  return common
    .requestJson({
      url: ApiClient.getUrl(
        '/AniSync/user?apiName=' + encodeURIComponent(provider) + '&userId=' + encodeURIComponent(userId),
      ),
    })
    .then(function (response) {
      clearPendingAuth(pageState);
      return refreshAdminData(view, pageState).then(function () {
        var successMessage =
          options.successMessage ||
          (response && response.name ? 'Connected as ' + response.name + '.' : 'Authentication succeeded.');
        pageState.common.setNotice(view.querySelector('#connectionCardNotice'), 'success', successMessage);
        return response;
      });
    })
    .catch(function (error) {
      return common.getErrorText(error).then(function (message) {
        if (options.autoRetry) {
          common.setNotice(
            view.querySelector('#connectionCardNotice'),
            'info',
            'Still waiting for the provider callback. If you already approved access, try again in a moment.',
          );
        } else {
          common.setNotice(view.querySelector('#connectionCardNotice'), 'error', message);
        }
        throw new Error(message);
      });
    })
    .finally(function () {
      common.setButtonBusy(button, false);
    });
}

function deauthenticateSelectedUser(view, pageState) {
  var common = pageState.common;
  var button = view.querySelector('#deauthenticate');
  var provider = getSelectedProvider(view);
  var userId = getSelectedUserId(view);

  common.setButtonBusy(button, true, 'Disconnecting...');
  common
    .requestVoid({
      url: ApiClient.getUrl(
        '/AniSync/deauthenticate?user=' + encodeURIComponent(userId) + '&apiName=' + encodeURIComponent(provider),
      ),
    })
    .then(function () {
      clearPendingAuth(pageState);
      hideAuthorizeLink(view);
      return refreshAdminData(view, pageState).then(function () {
        common.setNotice(
          view.querySelector('#connectionCardNotice'),
          'success',
          'Provider connection removed for the selected user.',
        );
      });
    })
    .catch(function (error) {
      common.getErrorText(error).then(function (message) {
        common.setNotice(view.querySelector('#connectionCardNotice'), 'error', message);
      });
    })
    .finally(function () {
      common.setButtonBusy(button, false);
    });
}

function maybeResumePendingAuth(view, pageState) {
  if (!hasPendingAuthForCurrentSelection(view, pageState)) {
    return;
  }

  testSelectedUserAuthentication(view, pageState, {
    autoRetry: true,
    suppressInfoNotice: false,
  }).catch(function () {
    // Keep the pending auth marker until the callback succeeds or the user disconnects.
  });
}

function clearPendingAuth(pageState) {
  pageState.pendingAuth = null;
  pageState.common.removeSessionValue(AniSyncAdminPage.pendingAuthKey);
}

function showAuthorizeLink(view, authorizeUrl) {
  var link = view.querySelector('#authorizeLink');
  link.href = authorizeUrl;
  link.classList.remove('aniSyncHidden');
}

function hideAuthorizeLink(view) {
  var link = view.querySelector('#authorizeLink');
  link.removeAttribute('href');
  link.classList.add('aniSyncHidden');
}

function hasPendingAuthForCurrentSelection(view, pageState) {
  if (!pageState.pendingAuth) {
    return false;
  }

  return (
    pageState.pendingAuth.provider === getSelectedProvider(view) &&
    pageState.pendingAuth.userId === getSelectedUserId(view)
  );
}

function buildConnectionSummary(provider, userAuth) {
  if (!userAuth) {
    return 'No stored authentication was found for this provider.';
  }

  if (provider === 'Annict') {
    return 'A personal access token is stored for this user.';
  }

  return 'Ani-Sync has a stored access token for this user.';
}

function validateProviderCredentials(view, definition) {
  var valid = true;
  if (!view.querySelector('#clientId').value) {
    setFieldError(view.querySelector('#providerClientIdError'), definition.providerClientIdLabel + ' is required.');
    valid = false;
  }

  if (definition.showProviderSecret && !view.querySelector('#clientSecret').value) {
    setFieldError(
      view.querySelector('#providerClientSecretError'),
      definition.providerClientSecretLabel + ' is required.',
    );
    valid = false;
  }

  return valid;
}

function validateUserCredentials(view, definition) {
  var valid = true;
  if (!view.querySelector('#userClientId').value) {
    setFieldError(view.querySelector('#userClientIdError'), definition.userClientIdLabel + ' is required.');
    valid = false;
  }

  if (definition.showUserSecret && !view.querySelector('#userClientSecret').value) {
    setFieldError(view.querySelector('#userClientSecretError'), definition.userClientSecretLabel + ' is required.');
    valid = false;
  }

  return valid;
}

function clearFieldErrors(view) {
  var errorIds = [
    '#providerClientIdError',
    '#providerClientSecretError',
    '#userClientIdError',
    '#userClientSecretError',
  ];

  for (var index = 0; index < errorIds.length; index++) {
    var element = view.querySelector(errorIds[index]);
    if (element) {
      element.textContent = '';
    }
  }
}

function setFieldError(element, message) {
  if (!element) {
    return;
  }

  element.textContent = message;
}

function setOptionalText(element, text) {
  setOptionalContent(element, text, false);
}

function setOptionalHtml(element, html) {
  setOptionalContent(element, html, true);
}

function setOptionalContent(element, content, useHtml) {
  if (!element) {
    return;
  }

  if (!content) {
    element.textContent = '';
    element.classList.add('aniSyncHidden');
    return;
  }

  if (useHtml) {
    element.innerHTML = content;
  } else {
    element.textContent = content;
  }

  element.classList.remove('aniSyncHidden');
}

function toggleElement(element, show) {
  if (!element) {
    return;
  }

  if (show) {
    element.classList.remove('aniSyncHidden');
  } else {
    element.classList.add('aniSyncHidden');
  }
}

function getSelectedProvider(view) {
  var select = view.querySelector('#selectProvider');
  return select ? select.value : '';
}

function getSelectedUserId(view) {
  var select = view.querySelector('#selectUser');
  return select ? select.value : '';
}

function getSelectedLibraryIds(view) {
  return pageStateSafeGetCheckedValues(view, '.library:checked');
}

function pageStateSafeGetCheckedValues(view, selector) {
  return Array.prototype.map.call(view.querySelectorAll(selector), function (element) {
    return element.getAttribute('id') || element.value;
  });
}

function getDefaultProvider(providerList) {
  return providerList && providerList.length ? providerList[0].Key : '';
}

function getDefaultUserId(users) {
  return users && users.length ? users[0].Id : '';
}

function getProviderDisplayName(pageState, providerKey) {
  var providerList = pageState && pageState.parameters ? pageState.parameters.providerList || [] : [];
  for (var index = 0; index < providerList.length; index++) {
    if (providerList[index].Key === providerKey) {
      return providerList[index].Name;
    }
  }

  return providerKey || 'Provider';
}

function getUserDisplayName(pageState, userId) {
  var users = pageState ? pageState.users || [] : [];
  for (var index = 0; index < users.length; index++) {
    if (users[index].Id === userId) {
      return users[index].Name;
    }
  }

  return 'Selected user';
}

function getSelectedUserConfiguration(view, pageState, createIfMissing) {
  return getUserConfiguration(pageState.config, getSelectedUserId(view), createIfMissing);
}

function getUserConfiguration(config, userId, createIfMissing) {
  if (!config) {
    return null;
  }

  for (var index = 0; index < config.UserConfig.length; index++) {
    if (config.UserConfig[index].UserId == userId) {
      return config.UserConfig[index];
    }
  }

  if (!createIfMissing) {
    return null;
  }

  var userConfig = createDefaultUserConfiguration(userId);
  config.UserConfig.push(userConfig);
  return userConfig;
}

function getOrCreateUserConfiguration(config, userId) {
  return getUserConfiguration(config, userId, true);
}

function createDefaultUserConfiguration(userId) {
  return {
    UserId: userId,
    UserApiAuth: [],
    LibraryToCheck: [],
    KeyPairs: [],
    PlanToWatchOnly: true,
    RewatchCompleted: true,
  };
}

function getProviderApiAuth(config, provider) {
  if (!config || !config.ProviderApiAuth) {
    return null;
  }

  for (var index = 0; index < config.ProviderApiAuth.length; index++) {
    if (config.ProviderApiAuth[index].Name === provider) {
      return config.ProviderApiAuth[index];
    }
  }

  return null;
}

function upsertProviderCredentials(config, provider, clientId, clientSecret) {
  var existing = getProviderApiAuth(config, provider);
  if (!clientId || !clientSecret) {
    removeProviderCredentials(config, provider);
    return;
  }

  if (existing) {
    existing.ClientId = clientId;
    existing.ClientSecret = clientSecret;
    return;
  }

  config.ProviderApiAuth.push({
    Name: provider,
    ClientId: clientId,
    ClientSecret: clientSecret,
  });
}

function removeProviderCredentials(config, provider) {
  if (!config || !config.ProviderApiAuth) {
    return;
  }

  config.ProviderApiAuth = config.ProviderApiAuth.filter(function (entry) {
    return entry.Name !== provider;
  });
}

function getUserApiAuth(userConfig, provider) {
  if (!userConfig || !userConfig.UserApiAuth) {
    return null;
  }

  for (var index = 0; index < userConfig.UserApiAuth.length; index++) {
    if (userConfig.UserApiAuth[index].Name === provider) {
      return userConfig.UserApiAuth[index];
    }
  }

  return null;
}

function upsertUserAuth(userConfig, provider, accessToken, refreshToken) {
  var existing = getUserApiAuth(userConfig, provider);
  if (existing) {
    existing.AccessToken = accessToken;
    existing.RefreshToken = refreshToken;
    return;
  }

  userConfig.UserApiAuth.push({
    Name: provider,
    AccessToken: accessToken,
    RefreshToken: refreshToken,
  });
}

function getConnectedProviderSet(userConfig) {
  var set = new Set();
  if (!userConfig || !userConfig.UserApiAuth) {
    return set;
  }

  for (var index = 0; index < userConfig.UserApiAuth.length; index++) {
    set.add(userConfig.UserApiAuth[index].Name);
  }

  return set;
}

function isProviderConfigured(view, definition) {
  if (!definition.requiresProviderCredentials) {
    return true;
  }

  return (
    !!view.querySelector('#clientId').value &&
    (!definition.showProviderSecret || !!view.querySelector('#clientSecret').value)
  );
}

function getProviderDefinition(provider) {
  switch (provider) {
    case 'Kitsu':
      return {
        requiresProviderCredentials: false,
        showProviderOAuth: false,
        usesUserCredentials: true,
        providerHint: 'No app credentials are needed. Each Emby user signs in with their own Kitsu account below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: '',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: '',
        showProviderSecret: false,
        userClientIdLabel: 'Kitsu username',
        userClientIdDescription: 'Used only for this sign-in.',
        userClientSecretLabel: 'Kitsu password',
        userClientSecretDescription: 'Used once to obtain tokens. Ani-Sync does not store the password.',
        showUserSecret: true,
        connectButtonText: 'Authenticate user',
        connectBusyText: 'Authenticating...',
        connectionHelp: "Enter the selected user's Kitsu login, then click Authenticate user.",
        disconnectedSummary: 'No Kitsu account is connected for this user.',
      };
    case 'Annict':
      return {
        requiresProviderCredentials: false,
        showProviderOAuth: false,
        usesUserCredentials: true,
        providerHint: 'No app credentials are needed. Paste a personal access token for the selected user below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: '',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: '',
        showProviderSecret: false,
        userClientIdLabel: 'Annict personal access token',
        userClientIdDescription: "Paste the selected user's token.",
        userClientSecretLabel: '',
        userClientSecretDescription: '',
        showUserSecret: false,
        connectButtonText: 'Save token and test',
        connectBusyText: 'Saving token...',
        connectionHelp: 'Paste the token, then click Save token and test.',
        disconnectedSummary: 'No Annict token is stored for this user.',
      };
    case 'Shikimori':
      return {
        requiresProviderCredentials: true,
        showProviderOAuth: true,
        usesUserCredentials: false,
        providerHint: 'Save the app credentials once, then connect individual Emby users below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: 'From your Shikimori application.',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: 'Stored in plain text in plugin configuration.',
        showProviderSecret: true,
        showShikimoriAppName: true,
        userClientIdLabel: '',
        userClientIdDescription: '',
        userClientSecretLabel: '',
        userClientSecretDescription: '',
        showUserSecret: false,
        connectButtonText: 'Open authorization page',
        connectBusyText: 'Preparing authorization...',
        connectionHelp: 'Click Open authorization page, approve access, then return here.',
        disconnectedSummary: 'No Shikimori connection is stored for this user.',
      };
    case 'Simkl':
      return {
        requiresProviderCredentials: true,
        showProviderOAuth: true,
        usesUserCredentials: false,
        providerHint: 'Save the app credentials once, then connect individual Emby users below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: 'From your Simkl application.',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: 'Stored in plain text in plugin configuration.',
        showProviderSecret: true,
        showSimklOptions: true,
        userClientIdLabel: '',
        userClientIdDescription: '',
        userClientSecretLabel: '',
        userClientSecretDescription: '',
        showUserSecret: false,
        connectButtonText: 'Open authorization page',
        connectBusyText: 'Preparing authorization...',
        connectionHelp: 'Click Open authorization page, approve access, then return here.',
        disconnectedSummary: 'No Simkl connection is stored for this user.',
      };
    case 'AniList':
      return {
        requiresProviderCredentials: true,
        showProviderOAuth: true,
        usesUserCredentials: false,
        providerHint: 'Save the app credentials once, then connect individual Emby users below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: 'From your AniList application.',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: 'Stored in plain text in plugin configuration.',
        showProviderSecret: true,
        userClientIdLabel: '',
        userClientIdDescription: '',
        userClientSecretLabel: '',
        userClientSecretDescription: '',
        showUserSecret: false,
        connectButtonText: 'Open authorization page',
        connectBusyText: 'Preparing authorization...',
        connectionHelp: 'Click Open authorization page, approve access, then return here.',
        disconnectedSummary: 'No AniList connection is stored for this user.',
      };
    case 'Mal':
    default:
      return {
        requiresProviderCredentials: true,
        showProviderOAuth: true,
        usesUserCredentials: false,
        providerHint: 'Save the app credentials once, then connect individual Emby users below.',
        providerClientIdLabel: 'Client ID',
        providerClientIdDescription: 'From your provider application.',
        providerClientSecretLabel: 'Client Secret',
        providerClientSecretDescription: 'Stored in plain text in plugin configuration.',
        showProviderSecret: true,
        userClientIdLabel: '',
        userClientIdDescription: '',
        userClientSecretLabel: '',
        userClientSecretDescription: '',
        showUserSecret: false,
        connectButtonText: 'Open authorization page',
        connectBusyText: 'Preparing authorization...',
        connectionHelp: 'Click Open authorization page, approve access, then return here.',
        disconnectedSummary: 'No provider connection is stored for this user.',
      };
  }
}

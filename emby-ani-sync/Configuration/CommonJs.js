define([], function () {
  function getTabs() {
    return [
      {
        href: configurationPageUrl('Ani-Sync'),
        name: 'General',
      },
      {
        href: configurationPageUrl('AniSync_ManualSync'),
        name: 'Manual Sync',
      },
    ];
  }

  function setTabs(selectedIndex, itemsFn) {
    var tabsContainer = document.querySelector('.pluginConfigurationPage:not(.hide) #navigationTabs');
    if (!tabsContainer) {
      return;
    }

    tabsContainer.innerHTML = '';

    var tabList = itemsFn();
    for (var index = 0; index < tabList.length; index++) {
      var tab = tabList[index];
      var element = document.createElement('a');
      element.innerHTML = tab.name;
      element.dataset.role = 'button';
      element.className = 'emby-button' + (index === selectedIndex ? ' ui-btn-active' : '');
      attachTabNavigation(element, tab);
      tabsContainer.appendChild(element);
    }
  }

  function attachTabNavigation(element, tab) {
    element.addEventListener('click', function () {
      Dashboard.navigate('/' + tab.href, false);
    });
  }

  function configurationPageUrl(name) {
    return 'configurationpage?name=' + encodeURIComponent(name);
  }

  function normalizeSet(values) {
    if (!values) {
      return null;
    }

    if (values instanceof Set) {
      return values;
    }

    var set = new Set();
    if (Array.isArray(values)) {
      for (var index = 0; index < values.length; index++) {
        set.add(values[index]);
      }
    }

    return set;
  }

  function setProviderSelection(page, providerList, providerListSelectElement, options) {
    options = options || {};
    var connectedSet = normalizeSet(options.connectedProviders);
    var select = page.querySelector(providerListSelectElement);
    var selectedValue = options.selectedValue || select.value;
    var html = '';

    for (var index = 0; index < providerList.length; index++) {
      var provider = providerList[index];
      var suffix = connectedSet && connectedSet.has(provider.Key) ? ' (connected)' : '';
      html += '<option value="' + provider.Key + '">' + provider.Name + suffix + '</option>';
    }

    select.innerHTML = html;
    if (selectedValue) {
      select.value = selectedValue;
    }
  }

  function populateUserList(page, users, userListSelectElement, selectedValue) {
    var html = '';
    for (var index = 0; index < users.length; index++) {
      html += '<option value="' + users[index].Id + '">' + users[index].Name + '</option>';
    }

    var select = page.querySelector(userListSelectElement);
    select.innerHTML = html;
    if (selectedValue) {
      select.value = selectedValue;
    }
  }

  function buildIncludesQuery(includes, extraParams) {
    var parts = [];
    if (includes && includes.length) {
      for (var index = 0; index < includes.length; index++) {
        parts.push('includes=' + encodeURIComponent(includes[index]));
      }
    }

    if (extraParams) {
      for (var key in extraParams) {
        if (!Object.prototype.hasOwnProperty.call(extraParams, key)) {
          continue;
        }

        if (extraParams[key] === undefined || extraParams[key] === null || extraParams[key] === '') {
          continue;
        }

        parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(extraParams[key]));
      }
    }

    return parts.join('&');
  }

  function normalizeRequestOptions(requestOptions, defaults) {
    var request =
      typeof requestOptions === 'string' ? { url: requestOptions } : Object.assign({}, requestOptions || {});

    var mergedDefaults = defaults || {};
    for (var key in mergedDefaults) {
      if (!Object.prototype.hasOwnProperty.call(mergedDefaults, key)) {
        continue;
      }

      if (request[key] === undefined) {
        request[key] = mergedDefaults[key];
      }
    }

    if (!request.type) {
      request.type = 'GET';
    }

    return request;
  }

  function requestJson(requestOptions) {
    return ApiClient.ajax(normalizeRequestOptions(requestOptions, { dataType: 'json' }));
  }

  function requestText(requestOptions) {
    return ApiClient.ajax(normalizeRequestOptions(requestOptions, { dataType: 'text' }));
  }

  function requestVoid(requestOptions) {
    return ApiClient.ajax(normalizeRequestOptions(requestOptions, { dataType: 'text' }));
  }

  function getErrorText(error) {
    if (typeof error === 'string') {
      return Promise.resolve(error);
    }

    if (!error) {
      return Promise.resolve('Unknown error.');
    }

    if (typeof error.message === 'string' && error.message) {
      return Promise.resolve(error.message);
    }

    if (typeof error.statusText === 'string' && error.statusText) {
      return Promise.resolve(error.statusText);
    }

    if (typeof error.text === 'function') {
      return error
        .text()
        .then(function (text) {
          return text || 'Unknown error.';
        })
        .catch(function () {
          return 'Unknown error.';
        });
    }

    return Promise.resolve(String(error));
  }

  function resolveElement(target) {
    if (typeof target === 'string') {
      return document.querySelector(target);
    }

    return target;
  }

  function clearNotice(target) {
    setNotice(target, null, '');
  }

  function setNotice(target, type, message) {
    var element = resolveElement(target);
    if (!element) {
      return;
    }

    if (!message) {
      element.innerHTML = '';
      element.className = 'aniSyncNotice is-hidden';
      return;
    }

    element.innerHTML = message;
    element.className = 'aniSyncNotice aniSyncNotice-' + (type || 'info');
  }

  function setButtonText(button, text) {
    if (!button) {
      return;
    }

    var textElement = button.querySelector('span');
    if (textElement) {
      textElement.textContent = text;
    } else {
      button.textContent = text;
    }
  }

  function getButtonText(button) {
    if (!button) {
      return '';
    }

    var textElement = button.querySelector('span');
    return textElement ? textElement.textContent : button.textContent;
  }

  function setButtonBusy(button, isBusy, busyText) {
    if (!button) {
      return;
    }

    if (!button.dataset.defaultText) {
      button.dataset.defaultText = getButtonText(button);
    }

    button.disabled = isBusy;
    setButtonText(button, isBusy ? busyText || 'Working...' : button.dataset.defaultText);
  }

  function renderLibraryCheckboxes(container, libraries, selectedIds, emptyText) {
    if (!container) {
      return;
    }

    selectedIds = selectedIds || [];
    var html = '<div data-role="controlgroup">';
    if (!libraries || libraries.length === 0) {
      html += '<p>' + (emptyText || 'No libraries found.') + '</p>';
    } else {
      for (var index = 0; index < libraries.length; index++) {
        var library = libraries[index];
        var checked = selectedIds.indexOf(library.Id) !== -1 ? ' checked="true"' : '';
        html +=
          '<label><input is="emby-checkbox" class="library" type="checkbox" data-mini="true" id="' +
          library.Id +
          '" name="' +
          library.Name +
          '"' +
          checked +
          ' /><span>' +
          library.Name +
          '</span></label>';
      }
    }

    html += '</div>';
    container.innerHTML = html;
  }

  function getCheckedValues(page, selector) {
    return Array.prototype.map.call(page.querySelectorAll(selector), function (element) {
      return element.getAttribute('id') || element.value;
    });
  }

  function setSessionValue(key, value) {
    try {
      sessionStorage.setItem(key, JSON.stringify(value));
    } catch (error) {
      console.warn('[AniSync] Failed to store session value', error);
    }
  }

  function getSessionValue(key) {
    try {
      var value = sessionStorage.getItem(key);
      return value ? JSON.parse(value) : null;
    } catch (error) {
      console.warn('[AniSync] Failed to read session value', error);
      return null;
    }
  }

  function removeSessionValue(key) {
    try {
      sessionStorage.removeItem(key);
    } catch (error) {
      console.warn('[AniSync] Failed to remove session value', error);
    }
  }

  function createPoller(pollFn, intervalMs) {
    var timeoutId = null;
    var active = false;

    function scheduleNext() {
      if (!active) {
        return;
      }

      timeoutId = setTimeout(function () {
        tick();
      }, intervalMs);
    }

    function tick() {
      if (!active) {
        return;
      }

      Promise.resolve(pollFn()).finally(scheduleNext);
    }

    return {
      start: function () {
        if (active) {
          return;
        }

        active = true;
        tick();
      },
      stop: function () {
        active = false;
        if (timeoutId) {
          clearTimeout(timeoutId);
          timeoutId = null;
        }
      },
      isActive: function () {
        return active;
      },
    };
  }

  function setStatusPill(target, tone, text) {
    var element = resolveElement(target);
    if (!element) {
      return;
    }

    element.textContent = text;
    element.className = 'aniSyncPill aniSyncPill-' + tone;
  }

  function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text);
    }

    return new Promise(function (resolve, reject) {
      try {
        var textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.opacity = '0';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        document.execCommand('copy');
        document.body.removeChild(textArea);
        resolve();
      } catch (error) {
        reject(error);
      }
    });
  }

  var TabGeneral = 0;
  var TabManualSync = 1;
  var parameterInclude = {
    ProviderList: 0,
    LocalIpAddress: 1,
    LocalPort: 2,
    Https: 3,
    Libraries: 4,
  };

  return {
    buildIncludesQuery: buildIncludesQuery,
    clearNotice: clearNotice,
    configurationPageUrl: configurationPageUrl,
    copyText: copyText,
    createPoller: createPoller,
    getCheckedValues: getCheckedValues,
    getErrorText: getErrorText,
    getSessionValue: getSessionValue,
    getTabs: getTabs,
    parameterInclude: parameterInclude,
    populateUserList: populateUserList,
    removeSessionValue: removeSessionValue,
    renderLibraryCheckboxes: renderLibraryCheckboxes,
    requestJson: requestJson,
    requestText: requestText,
    requestVoid: requestVoid,
    setButtonBusy: setButtonBusy,
    setNotice: setNotice,
    setProviderSelection: setProviderSelection,
    setSessionValue: setSessionValue,
    setStatusPill: setStatusPill,
    setTabs: setTabs,
    TabGeneral: TabGeneral,
    TabManualSync: TabManualSync,
  };
});

// Per-user theme (viewer layer): the choice rides a cookie, the page reloads, and the
// server-rendered head links the chosen theme (App.razor reads the same cookie). The
// host admin's color_scheme stays the default for viewers without a cookie.
(function () {
  'use strict';

  var COOKIE = 'dashspec_color_scheme';
  var YEAR = 60 * 60 * 24 * 365;

  function read() {
    var m = document.cookie.match(new RegExp('(?:^|;\\s*)' + COOKIE + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : '';
  }

  window.dashspecTheme = {
    set: function (value) {
      if (value) {
        document.cookie = COOKIE + '=' + encodeURIComponent(value) + ';path=/;max-age=' + YEAR + ';SameSite=Lax';
      } else {
        document.cookie = COOKIE + '=;path=/;max-age=0;SameSite=Lax';
      }
      window.location.reload();
    },
    // Reflect the stored choice in the picker (the static pass can't read cookies).
    init: function () {
      var value = read();
      document.querySelectorAll('[data-theme-picker]').forEach(function (sel) {
        sel.value = value || '';
      });
    },
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.dashspecTheme.init);
  } else {
    window.dashspecTheme.init();
  }
})();

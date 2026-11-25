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

  // Fallback Doxygen documentation portal scroll-spy & section scroll helper (ADR-0138)
  if (!window.dashSpecDocPortal) {
    window.dashSpecDocPortal = (function () {
      var currentContainer = null;
      var onScrollHandler = null;
      var rafId = null;
      var pinnedId = null;
      var pinnedTimer = null;

      function setActiveTocId(activeId) {
        var links = document.querySelectorAll('.dox-toc [data-dox-toc-target]');
        for (var i = 0; i < links.length; i++) {
          var el = links[i];
          var target = el.getAttribute('data-dox-toc-target');
          if (target && target === activeId) {
            el.classList.add('is-active');
          } else {
            el.classList.remove('is-active');
          }
        }
      }

      function updateActiveOnScroll() {
        rafId = null;
        if (pinnedId) {
          setActiveTocId(pinnedId);
          return;
        }

        var main = document.querySelector('.dox-main');
        if (!main) return;

        var anchors = main.querySelectorAll('[data-dox-sec]');
        if (!anchors || anchors.length === 0) return;

        var mainRect = main.getBoundingClientRect();
        var threshold = mainRect.top + 88;
        var activeId = anchors[0].getAttribute('data-dox-sec');

        for (var i = 0; i < anchors.length; i++) {
          var a = anchors[i];
          var rect = a.getBoundingClientRect();
          if (rect.top <= threshold) {
            activeId = a.getAttribute('data-dox-sec');
          } else {
            break;
          }
        }

        if (activeId) {
          setActiveTocId(activeId);
        }
      }

      function bindScrollSpy() {
        var main = document.querySelector('.dox-main');
        if (!main) return;

        if (currentContainer !== main) {
          if (currentContainer && onScrollHandler) {
            currentContainer.removeEventListener('scroll', onScrollHandler);
          }
          currentContainer = main;
          onScrollHandler = function () {
            if (!rafId) {
              rafId = window.requestAnimationFrame(updateActiveOnScroll);
            }
          };
          currentContainer.addEventListener('scroll', onScrollHandler, { passive: true });
        }

        updateActiveOnScroll();
      }

      function scrollToSection(sectionId) {
        var main = document.querySelector('.dox-main');
        if (!main || !sectionId) return;

        var target = document.getElementById(sectionId) ||
                     main.querySelector('[data-dox-sec="' + sectionId + '"]');
        if (!target) return;

        pinnedId = sectionId;
        if (pinnedTimer) window.clearTimeout(pinnedTimer);
        pinnedTimer = window.setTimeout(function () {
          pinnedId = null;
        }, 550);

        setActiveTocId(sectionId);
        var mainRect = main.getBoundingClientRect();
        var targetRect = target.getBoundingClientRect();
        var topOffset = targetRect.top - mainRect.top + main.scrollTop - 16;
        main.scrollTo({ top: Math.max(0, topOffset), behavior: 'smooth' });
      }

      return {
        initScrollSpy: bindScrollSpy,
        scrollToSection: scrollToSection,
      };
    })();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.dashspecTheme.init);
  } else {
    window.dashspecTheme.init();
  }
})();

(function () {
  "use strict";
  // Thin adapter only: display + calendar are flatpickr's job, parsing is C#'s
  // job (DateValueCodec.TryParseDay — culture-aware, tolerant). No hand-rolled
  // date logic here: wire conversion uses flatpickr.formatDate/parseDate.
  var instances = new WeakMap();

  function init(el, dotNetRef, opts) {
    dispose(el);
    opts = opts || {};
    var mode = opts.mode || "day";
    var wireFormat = opts.wireFormat || "Y-m-d";
    var cfg = {
      allowInput: true,
      disableMobile: true,
      locale: opts.locale || "default",
      dateFormat: opts.dateFormat || "Y-m-d"
    };
    if (typeof opts.weekStart === "number") cfg.weekStart = opts.weekStart;
    if (opts.minDate) cfg.minDate = opts.minDate;
    if (opts.maxDate) cfg.maxDate = opts.maxDate;

    if (mode === "month") {
      cfg.dateFormat = "Y-m";
      cfg.altFormat = opts.altFormat || "F Y";
      cfg.plugins = [window.monthSelectPlugin({
        shorthand: false,
        dateFormat: cfg.dateFormat,
        altFormat: cfg.altFormat,
        theme: "light"
      })];
    } else if (mode === "time") {
      cfg.enableTime = true;
      cfg.noCalendar = true;
      cfg.time_24hr = opts.time24h !== false;
      cfg.dateFormat = opts.dateFormat || "H:i";
    } else if (mode === "datetime") {
      cfg.enableTime = true;
      cfg.time_24hr = opts.time24h !== false;
      cfg.dateFormat = opts.dateFormat || "Y-m-d H:i";
    }

    cfg.onChange = function (dates) {
      if (dates && dates.length) {
        dotNetRef.invokeMethodAsync("OnPicked", window.flatpickr.formatDate(dates[0], wireFormat));
      }
    };
    cfg.onClose = function (dates, dateStr, instance) {
      if (!instance.input.value) {
        dotNetRef.invokeMethodAsync("OnPicked", "");
        return;
      }
      if (dates && dates.length) return;
      // Untyped-but-not-matched text: let C# parse tolerantly (DateValueCodec).
      dotNetRef.invokeMethodAsync("OnRawInput", instance.input.value);
    };

    var fp = window.flatpickr(el, cfg);
    if (opts.wire) {
      var parsed = window.flatpickr.parseDate(opts.wire, wireFormat);
      if (parsed) fp.setDate(parsed, false);
    }
    instances.set(el, { fp: fp, wireFormat: wireFormat });
  }

  function update(el, wire) {
    var st = instances.get(el);
    if (!st) return;
    var d = wire ? window.flatpickr.parseDate(wire, st.wireFormat) : null;
    st.fp.setDate(d || "", false);
  }

  function dispose(el) {
    var st = instances.get(el);
    if (st) {
      try { st.fp.destroy(); } catch (e) { }
      instances.delete(el);
    }
  }

  window.dashspecDatepicker = { init: init, update: update, dispose: dispose };
})();

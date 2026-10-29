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
    } else if (mode === "range") {
      cfg.mode = "range";
      // No altInput: the original input is Blazor-owned (disabled syncs on re-render);
      // an alt-input copies the disabled flag at init and then drifts.
      cfg.allowInput = false;
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

    // Commit once — on close (calendar pick done, blur, or Enter), never per
    // keystroke: each OnPicked triggers a full data reload downstream.
    cfg.onClose = function (dates, dateStr, instance) {
      if (cfg.mode === "range") {
        if (dates && dates.length === 2) {
          dotNetRef.invokeMethodAsync("OnPickedRange",
            window.flatpickr.formatDate(dates[0], wireFormat),
            window.flatpickr.formatDate(dates[1], wireFormat));
        } else if (dates && dates.length === 1) {
          var single = window.flatpickr.formatDate(dates[0], wireFormat);
          dotNetRef.invokeMethodAsync("OnPickedRange", single, single);
        } else if (!instance.input.value) {
          dotNetRef.invokeMethodAsync("OnPickedRange", "", "");
        }
        return;
      }
      if (!instance.input.value) {
        dotNetRef.invokeMethodAsync("OnPicked", "");
        return;
      }
      if (dates && dates.length) {
        dotNetRef.invokeMethodAsync("OnPicked", window.flatpickr.formatDate(dates[0], wireFormat));
        return;
      }
      // Untyped-but-not-matched text: let C# parse tolerantly (DateValueCodec).
      dotNetRef.invokeMethodAsync("OnRawInput", instance.input.value);
    };

    var fp = window.flatpickr(el, cfg);
    if (mode === "range") {
      var from = opts.wireFrom ? window.flatpickr.parseDate(opts.wireFrom, wireFormat) : null;
      var to = opts.wireTo ? window.flatpickr.parseDate(opts.wireTo, wireFormat) : null;
      if (from && to) fp.setDate([from, to], false);
      else if (from) fp.setDate(from, false);
    } else if (opts.wire) {
      var parsed = window.flatpickr.parseDate(opts.wire, wireFormat);
      if (parsed) fp.setDate(parsed, false);
    }
    // Enter closes the calendar/input -> onClose commits exactly once.
    el.addEventListener("keydown", function (e) {
      if (e.key === "Enter") {
        e.preventDefault();
        fp.close();
      }
    });
    instances.set(el, { fp: fp, wireFormat: wireFormat });
  }

  function update(el, wire) {
    var st = instances.get(el);
    if (!st) return;
    var d = wire ? window.flatpickr.parseDate(wire, st.wireFormat) : null;
    st.fp.setDate(d || "", false);
  }

  function updateRange(el, fromWire, toWire) {
    var st = instances.get(el);
    if (!st) return;
    var from = fromWire ? window.flatpickr.parseDate(fromWire, st.wireFormat) : null;
    var to = toWire ? window.flatpickr.parseDate(toWire, st.wireFormat) : null;
    if (from && to) st.fp.setDate([from, to], false);
    else st.fp.setDate(from || "", false);
  }

  function dispose(el) {
    var st = instances.get(el);
    if (st) {
      try { st.fp.destroy(); } catch (e) { }
      instances.delete(el);
    }
  }

  window.dashspecDatepicker = { init: init, update: update, updateRange: updateRange, dispose: dispose };
})();

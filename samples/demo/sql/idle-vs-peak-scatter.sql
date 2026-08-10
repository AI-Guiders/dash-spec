SELECT
  i.usage_date,
  i.user_id,
  i.app_name,
  CAST(i.idle_minutes AS float) AS idle_minutes,
  CAST(h.peak_concurrent_apps AS float) AS peak_concurrent_apps
FROM demo.v_daily_idle_minutes_by_user_app AS i
INNER JOIN demo.v_daily_peak_concurrent_apps_per_user AS h
  ON i.usage_date = h.usage_date
 AND i.user_id = h.user_id

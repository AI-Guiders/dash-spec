-- DashSpec demo soak bootstrap (local SQL Server).
-- Creates fictional demo schema + ~14 days of synthetic activity for samples/demo/demo-soak.dashspec.
-- Usage: sqlcmd -S localhost -d DashSpecDemo -E -i bootstrap-demo-soak.sql

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'demo')
    EXEC(N'CREATE SCHEMA demo');
GO

IF OBJECT_ID(N'demo.Events', N'U') IS NOT NULL DROP TABLE demo.Events;
GO

CREATE TABLE demo.Events
(
    event_id           bigint        IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    occurred_at_utc    datetime2(0)  NOT NULL,
    app_name           nvarchar(128) NOT NULL,
    user_id            nvarchar(64)  NOT NULL,
    user_name          nvarchar(128) NOT NULL,
    product_version    nvarchar(32)  NOT NULL,
    window_title       nvarchar(256) NOT NULL
);
GO

CREATE INDEX IX_demo_Events_occurred ON demo.Events (occurred_at_utc) INCLUDE (app_name, user_id);
GO

DECLARE @today date = CAST(SYSUTCDATETIME() AS date);
DECLARE @day int = 0;
DECLARE @apps table (app_name nvarchar(128));
DECLARE @users table (user_id nvarchar(64), user_name nvarchar(128));

INSERT INTO @apps (app_name) VALUES
    (N'Tekla Structures'),
    (N'Revit'),
    (N'AutoCAD'),
    (N'Excel'),
    (N'Teams');

INSERT INTO @users (user_id, user_name) VALUES
    (N'u.alice', N'Alice Chen'),
    (N'u.bob', N'Bob Rivera'),
    (N'u.carol', N'Carol Nguyen'),
    (N'u.dave', N'Dave Okonkwo'),
    (N'u.erin', N'Erin Walsh'),
    (N'u.frank', N'Frank Meyer');

WHILE @day < 14
BEGIN
    DECLARE @usage date = DATEADD(day, -@day, @today);
    DECLARE @slot int = 0;

    WHILE @slot < 96
    BEGIN
        DECLARE @bucket datetime2(0) = DATEADD(minute, @slot * 15, CAST(@usage AS datetime2(0)));
        DECLARE @u int = 1 + (@day * 3 + @slot) % 6;
        DECLARE @a int = 1 + (@day + @slot) % 5;
        DECLARE @user_id nvarchar(64);
        DECLARE @user_name nvarchar(128);
        DECLARE @app_name nvarchar(128);
        DECLARE @events int = 1 + (@slot + @day) % 4;

        SELECT @user_id = user_id, @user_name = user_name
        FROM (
            SELECT user_id, user_name, ROW_NUMBER() OVER (ORDER BY user_id) AS rn
            FROM @users
        ) q
        WHERE rn = @u;

        SELECT @app_name = app_name
        FROM (
            SELECT app_name, ROW_NUMBER() OVER (ORDER BY app_name) AS rn
            FROM @apps
        ) q
        WHERE rn = @a;

        WHILE @events > 0
        BEGIN
            INSERT INTO demo.Events (occurred_at_utc, app_name, user_id, user_name, product_version, window_title)
            VALUES
            (
                DATEADD(minute, (@events * 2) % 15, @bucket),
                @app_name,
                @user_id,
                @user_name,
                CONCAT(N'2026.', 1 + (@day % 3), N'.', 10 + (@slot % 20)),
                CONCAT(@app_name, N' — ', CASE @slot % 4 WHEN 0 THEN N'Model review' WHEN 1 THEN N'Export' WHEN 2 THEN N'Sync' ELSE N'Dashboard' END)
            );
            SET @events -= 1;
        END;

        IF @slot % 11 = 0
        BEGIN
            INSERT INTO demo.Events (occurred_at_utc, app_name, user_id, user_name, product_version, window_title)
            SELECT
                DATEADD(minute, 7, @bucket),
                a.app_name,
                @user_id,
                @user_name,
                N'2026.1.0',
                CONCAT(a.app_name, N' — background')
            FROM @apps a
            WHERE a.app_name <> @app_name
              AND (ABS(CHECKSUM(a.app_name, @usage, @slot)) % 3) = 0;
        END;

        SET @slot += 1;
    END;

    SET @day += 1;
END;
GO

CREATE OR ALTER VIEW demo.v_events_detail
AS
SELECT
    e.occurred_at_utc,
    e.app_name,
    e.user_id,
    e.user_name,
    e.product_version,
    e.window_title
FROM demo.Events AS e;
GO

CREATE OR ALTER VIEW demo.v_five_minute_activity
AS
WITH buckets AS
(
    SELECT
        e.occurred_at_utc,
        CAST(e.occurred_at_utc AS date) AS usage_date,
        e.user_id,
        e.app_name,
        DATEADD(
            minute,
            (DATEDIFF(minute, CAST(CAST(e.occurred_at_utc AS date) AS datetime2(0)), e.occurred_at_utc) / 5) * 5,
            CAST(CAST(e.occurred_at_utc AS date) AS datetime2(0))) AS bucket_start_utc
    FROM demo.Events AS e
)
SELECT
    b.bucket_start_utc,
    b.usage_date,
    b.user_id,
    b.app_name,
    COUNT_BIG(*) AS event_count
FROM buckets AS b
GROUP BY b.bucket_start_utc, b.usage_date, b.user_id, b.app_name;
GO

CREATE OR ALTER VIEW demo.v_daily_active_users
AS
SELECT
    CAST(e.occurred_at_utc AS date) AS usage_date,
    e.user_id,
    e.app_name,
    CAST(1 AS int) AS distinct_users
FROM demo.Events AS e
GROUP BY CAST(e.occurred_at_utc AS date), e.user_id, e.app_name;
GO

CREATE OR ALTER VIEW demo.v_daily_peak_concurrent_proxy
AS
WITH hourly AS
(
    SELECT
        CAST(e.occurred_at_utc AS date) AS usage_date,
        e.user_id,
        e.app_name,
        DATEPART(hour, e.occurred_at_utc) AS usage_hour,
        COUNT_BIG(*) AS event_count
    FROM demo.Events AS e
    GROUP BY CAST(e.occurred_at_utc AS date), e.user_id, e.app_name, DATEPART(hour, e.occurred_at_utc)
),
ranked AS
(
    SELECT
        h.*,
        ROW_NUMBER() OVER (
            PARTITION BY h.usage_date, h.user_id, h.app_name
            ORDER BY h.event_count DESC, h.usage_hour) AS rn
    FROM hourly AS h
)
SELECT
    r.usage_date,
    r.user_id,
    r.app_name,
    CAST(r.event_count AS int) AS peak_concurrent_proxy
FROM ranked AS r
WHERE r.rn = 1;
GO

CREATE OR ALTER VIEW demo.v_peak_concurrent_by_period
AS
SELECT
    CAST(N'day' AS nvarchar(16)) AS period_grain,
    CAST(e.occurred_at_utc AS date) AS period_start,
    e.app_name,
    CAST(MAX(x.peak_concurrent_proxy) AS int) AS peak_concurrent_proxy
FROM demo.Events AS e
INNER JOIN demo.v_daily_peak_concurrent_proxy AS x
    ON x.usage_date = CAST(e.occurred_at_utc AS date)
   AND x.user_id = e.user_id
   AND x.app_name = e.app_name
GROUP BY CAST(e.occurred_at_utc AS date), e.app_name

UNION ALL

SELECT
    CAST(N'month' AS nvarchar(16)) AS period_grain,
    DATEFROMPARTS(YEAR(e.occurred_at_utc), MONTH(e.occurred_at_utc), 1) AS period_start,
    e.app_name,
    CAST(MAX(x.peak_concurrent_proxy) AS int) AS peak_concurrent_proxy
FROM demo.Events AS e
INNER JOIN demo.v_daily_peak_concurrent_proxy AS x
    ON x.usage_date = CAST(e.occurred_at_utc AS date)
   AND x.user_id = e.user_id
   AND x.app_name = e.app_name
GROUP BY DATEFROMPARTS(YEAR(e.occurred_at_utc), MONTH(e.occurred_at_utc), 1), e.app_name

UNION ALL

SELECT
    CAST(N'year' AS nvarchar(16)) AS period_grain,
    DATEFROMPARTS(YEAR(e.occurred_at_utc), 1, 1) AS period_start,
    e.app_name,
    CAST(MAX(x.peak_concurrent_proxy) AS int) AS peak_concurrent_proxy
FROM demo.Events AS e
INNER JOIN demo.v_daily_peak_concurrent_proxy AS x
    ON x.usage_date = CAST(e.occurred_at_utc AS date)
   AND x.user_id = e.user_id
   AND x.app_name = e.app_name
GROUP BY YEAR(e.occurred_at_utc), e.app_name;
GO

CREATE OR ALTER VIEW demo.v_daily_peak_concurrent_apps_per_user
AS
WITH daily AS
(
    SELECT
        CAST(e.occurred_at_utc AS date) AS usage_date,
        e.user_id,
        e.app_name,
        COUNT_BIG(*) AS hits
    FROM demo.Events AS e
    GROUP BY CAST(e.occurred_at_utc AS date), e.user_id, e.app_name
),
ranked AS
(
    SELECT
        d.usage_date,
        d.user_id,
        d.app_name,
        d.hits,
        ROW_NUMBER() OVER (PARTITION BY d.usage_date, d.user_id ORDER BY d.hits DESC, d.app_name) AS rn
    FROM daily AS d
)
SELECT
    r.usage_date,
    r.user_id,
    CAST(COUNT(*) AS int) AS peak_concurrent_apps,
    STRING_AGG(r.app_name, N', ') WITHIN GROUP (ORDER BY r.rn) AS peak_apps
FROM ranked AS r
WHERE r.rn <= 4
GROUP BY r.usage_date, r.user_id;
GO

CREATE OR ALTER VIEW demo.v_daily_idle_minutes_by_user_app
AS
WITH spans AS
(
    SELECT
        CAST(e.occurred_at_utc AS date) AS usage_date,
        e.user_id,
        e.app_name,
        DATEDIFF(
            minute,
            LAG(e.occurred_at_utc) OVER (PARTITION BY CAST(e.occurred_at_utc AS date), e.user_id, e.app_name ORDER BY e.occurred_at_utc),
            e.occurred_at_utc) AS gap_minutes
    FROM demo.Events AS e
)
SELECT
    s.usage_date,
    s.user_id,
    s.app_name,
    CAST(SUM(CASE WHEN s.gap_minutes IS NULL OR s.gap_minutes > 30 THEN 15 ELSE s.gap_minutes END) AS int) AS idle_minutes
FROM spans AS s
GROUP BY s.usage_date, s.user_id, s.app_name;
GO

SELECT
    (SELECT COUNT(*) FROM demo.Events) AS events,
    (SELECT COUNT(*) FROM demo.v_daily_peak_concurrent_proxy) AS peak_rows,
    (SELECT COUNT(*) FROM demo.v_events_detail) AS detail_rows;
GO

-- ai_landscape/by_country_2024
CREATE OR ALTER VIEW dashspec_ai_landscape_by_country_2024_fee3bde129a1 AS
SELECT country AS country, COUNT(*) AS value
FROM (
SELECT r.org_id AS [r.org_id], r.name AS [r.name], r.country, r.kind, l.model_id, l.org_id, l.name, l.domain, l.released, l.parameters_b, l.training_flop, l.cost_musd, l.accessibility
FROM ai_landscape.models_2024 AS l INNER JOIN ai_landscape.orgs AS r
ON l.org_id = r.org_id
) AS src
GROUP BY country;

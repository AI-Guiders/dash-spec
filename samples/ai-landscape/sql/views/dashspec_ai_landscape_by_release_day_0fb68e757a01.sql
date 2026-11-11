-- ai_landscape/by_release_day
CREATE OR ALTER VIEW dashspec_ai_landscape_by_release_day_0fb68e757a01 AS
SELECT released AS released, COUNT(*) AS value
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2023
UNION ALL
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2024
) AS u
) AS src
GROUP BY released;

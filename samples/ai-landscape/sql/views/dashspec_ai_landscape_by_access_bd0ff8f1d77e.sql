-- ai_landscape/by_access
CREATE OR ALTER VIEW dashspec_ai_landscape_by_access_bd0ff8f1d77e AS
SELECT accessibility AS accessibility, COUNT(*) AS value
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2023
UNION ALL
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2024
) AS u
) AS src
GROUP BY accessibility;

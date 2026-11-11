-- ai_landscape/by_domain
CREATE OR ALTER VIEW dashspec_ai_landscape_by_domain_e137531b6cb6 AS
SELECT domain AS domain, COUNT(*) AS value
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility
FROM (
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2023
UNION ALL
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility FROM ai_landscape.models_2024
) AS u
) AS src
GROUP BY domain;

-- ai_landscape/models_2023
CREATE OR ALTER VIEW dashspec_ai_landscape_models_2023_e42b94b5344d AS
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility
FROM ai_landscape.models_2023;

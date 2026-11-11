-- ai_landscape/models_2024
CREATE OR ALTER VIEW dashspec_ai_landscape_models_2024_e720edf7459b AS
SELECT model_id, org_id, name, domain, released, parameters_b, training_flop, cost_musd, accessibility
FROM ai_landscape.models_2024;

using '../main.bicep'

param location = 'brazilsouth'
param environmentName = 'prod'
param resourceGroupName = 'rg-nexora-prod'
param resourceNamePrefix = 'nexora-prod'
param imageTag = '0000000000000000000000000000000000000000'
param postgresAdministratorLogin = 'nexora_app'
param postgresAdministratorPassword = readEnvironmentVariable('NEXORA_POSTGRES_ADMIN_PASSWORD')
param frontendRepositoryUrl = 'https://github.com/OWNER/REPOSITORY'
param alertEmail = 'operations@example.invalid'
param apiHost = 'api.example.invalid'
param frontendOrigin = 'https://app.example.invalid'
param emailFromAddress = 'noreply@example.invalid'
param deployWorkloads = false

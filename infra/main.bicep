targetScope = 'subscription'

@description('Azure region. Brazil South is the preferred starting point when all selected services are available.')
param location string = 'brazilsouth'
param environmentName string = 'prod'
param resourceGroupName string = 'rg-nexora-${environmentName}'
param resourceNamePrefix string = 'nexora-${environmentName}'
param imageTag string
param postgresAdministratorLogin string
@secure()
param postgresAdministratorPassword string
param frontendRepositoryUrl string = ''
param alertEmail string
param apiHost string
param frontendOrigin string
param emailFromAddress string
param deployWorkloads bool = false

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: { application: 'nexora', environment: environmentName, managedBy: 'bicep' }
}

module platform 'modules/platform.bicep' = {
  name: 'nexora-${environmentName}-${imageTag}'
  scope: resourceGroup
  params: {
    location: location
    prefix: resourceNamePrefix
    imageTag: imageTag
    postgresAdministratorLogin: postgresAdministratorLogin
    postgresAdministratorPassword: postgresAdministratorPassword
    frontendRepositoryUrl: frontendRepositoryUrl
    alertEmail: alertEmail
    apiHost: apiHost
    frontendOrigin: frontendOrigin
    emailFromAddress: emailFromAddress
    deployWorkloads: deployWorkloads
  }
}

output apiFqdn string = platform.outputs.apiFqdn
output staticWebAppHostname string = platform.outputs.staticWebAppHostname
output registryName string = platform.outputs.registryName
output keyVaultName string = platform.outputs.keyVaultName
output migrationJobName string = platform.outputs.migrationJobName

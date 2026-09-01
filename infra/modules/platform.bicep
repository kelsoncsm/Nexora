param location string
param prefix string
param imageTag string
param postgresAdministratorLogin string
@secure()
param postgresAdministratorPassword string
param frontendRepositoryUrl string
param alertEmail string
param apiHost string
param frontendOrigin string
param emailFromAddress string
param deployWorkloads bool

var compact = toLower(replace(prefix, '-', ''))
var unique = take(uniqueString(resourceGroup().id), 6)
var acrName = take('${compact}${unique}', 50)
var vaultName = take('${prefix}-kv-${unique}', 24)
var identityName = '${prefix}-api-identity'
var acrPullRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var keyVaultSecretsUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource vnet 'Microsoft.Network/virtualNetworks@2024-07-01' = {
  name: '${prefix}-vnet'
  location: location
  properties: {
    addressSpace: { addressPrefixes: [ '10.20.0.0/16' ] }
  }
}
resource containerSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-07-01' = {
  parent: vnet
  name: 'container-apps'
  properties: { addressPrefix: '10.20.0.0/23', delegations: [ { name: 'Microsoft.App-environments', properties: { serviceName: 'Microsoft.App/environments' } } ] }
}
resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-07-01' = {
  parent: vnet
  name: 'postgres'
  properties: { addressPrefix: '10.20.2.0/28', delegations: [ { name: 'Microsoft.DBforPostgreSQL-flexibleServers', properties: { serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers' } } ] }
}

resource postgresDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: '${prefix}.postgres.database.azure.com'
  location: 'global'
}
resource postgresDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: postgresDns
  name: '${prefix}-postgres-link'
  location: 'global'
  properties: { registrationEnabled: false, virtualNetwork: { id: vnet.id } }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${prefix}-postgres-${unique}'
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '18'
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorPassword
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 14, geoRedundantBackup: 'Disabled' }
    network: { delegatedSubnetResourceId: postgresSubnet.id, privateDnsZoneArmResourceId: postgresDns.id, publicNetworkAccess: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
  }
  dependsOn: [ postgresDnsLink ]
}
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'nexora'
  properties: { charset: 'UTF8', collation: 'en_US.utf8' }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false, publicNetworkAccess: 'Enabled', policies: { retentionPolicy: { days: 7, status: 'enabled' } } }
}
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, acrPullRole)
  scope: registry
  properties: { roleDefinitionId: acrPullRole, principalId: identity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  properties: { tenantId: tenant().tenantId, sku: { family: 'A', name: 'standard' }, enableRbacAuthorization: true, enablePurgeProtection: true, softDeleteRetentionInDays: 90, publicNetworkAccess: 'Enabled' }
}
resource vaultRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, keyVaultSecretsUserRole)
  scope: vault
  properties: { roleDefinitionId: keyVaultSecretsUserRole, principalId: identity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: { retentionInDays: 30, features: { enableLogAccessUsingOnlyResourcePermissions: true } }
}
resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-insights'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id, DisableLocalAuth: false }
}
resource environment 'Microsoft.App/managedEnvironments@2024-10-02-preview' = {
  name: '${prefix}-aca-env'
  location: location
  properties: {
    appLogsConfiguration: { destination: 'log-analytics', logAnalyticsConfiguration: { customerId: logs.properties.customerId, sharedKey: logs.listKeys().primarySharedKey } }
    vnetConfiguration: { infrastructureSubnetId: containerSubnet.id, internal: false }
    appInsightsConfiguration: { connectionString: insights.properties.ConnectionString }
    openTelemetryConfiguration: { tracesConfiguration: { destinations: [ 'appInsights' ] }, logsConfiguration: { destinations: [ 'appInsights' ] } }
  }
}

resource api 'Microsoft.App/containerApps@2025-02-02-preview' = if (deployWorkloads) {
  name: '${prefix}-api'
  location: location
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identity.id}': {} } }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: { external: true, targetPort: 8080, allowInsecure: false, transport: 'auto' }
      registries: [ { server: registry.properties.loginServer, identity: identity.id } ]
      secrets: [
        { name: 'database', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-db-connection', identity: identity.id }
        { name: 'jwt', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-jwt-signing-key', identity: identity.id }
        { name: 'mercadopago-token', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-mercadopago-access-token', identity: identity.id }
        { name: 'mercadopago-webhook', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-mercadopago-webhook-secret', identity: identity.id }
        { name: 'resend', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-resend-api-key', identity: identity.id }
      ]
    }
    template: {
      revisionSuffix: take(imageTag, 10)
      containers: [ {
        name: 'api'
        image: '${registry.properties.loginServer}/nexora-api:${imageTag}'
        resources: { cpu: json('0.5'), memory: '1Gi' }
        env: [
          { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
          { name: 'ConnectionStrings__NexoraDatabase', secretRef: 'database' }
          { name: 'Identity__SigningKey', secretRef: 'jwt' }
          { name: 'Payments__MercadoPago__AccessToken', secretRef: 'mercadopago-token' }
          { name: 'Payments__MercadoPago__WebhookSecret', secretRef: 'mercadopago-webhook' }
          { name: 'Email__Provider', value: 'Resend' }
          { name: 'Email__ResendApiKey', secretRef: 'resend' }
          { name: 'Email__WorkerEnabled', value: 'true' }
          { name: 'Email__FromAddress', value: emailFromAddress }
          { name: 'Email__ApplicationUrl', value: frontendOrigin }
          { name: 'Cors__AllowedOrigins__0', value: frontendOrigin }
          { name: 'AllowedHosts', value: '${apiHost};*.azurecontainerapps.io;localhost' }
          { name: 'ReverseProxy__KnownNetworks__0', value: '10.20.0.0/23' }
          { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
          { name: 'OTEL_SERVICE_NAME', value: 'nexora-api' }
        ]
        probes: [
          { type: 'Liveness', httpGet: { path: '/health/live', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 15, periodSeconds: 30 }
          { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 10, periodSeconds: 15 }
        ]
      } ]
      scale: { minReplicas: 1, maxReplicas: 3, rules: [ { name: 'http', http: { metadata: { concurrentRequests: '50' } } } ] }
    }
  }
  dependsOn: [ acrPull, vaultRead ]
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = if (deployWorkloads) {
  name: '${prefix}-migrate'
  location: location
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${identity.id}': {} } }
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      registries: [ { server: registry.properties.loginServer, identity: identity.id } ]
      secrets: [ { name: 'database', keyVaultUrl: '${vault.properties.vaultUri}secrets/nexora-db-connection', identity: identity.id } ]
    }
    template: { containers: [ { name: 'migrate', image: '${registry.properties.loginServer}/nexora-api:${imageTag}', command: [ '/app/migrate' ], resources: { cpu: json('0.5'), memory: '1Gi' }, env: [ { name: 'ConnectionStrings__NexoraDatabase', secretRef: 'database' } ] } ] }
  }
  dependsOn: [ acrPull, vaultRead ]
}

resource staticWeb 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${prefix}-web'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: { repositoryUrl: empty(frontendRepositoryUrl) ? null : frontendRepositoryUrl, branch: 'main', provider: empty(frontendRepositoryUrl) ? 'None' : 'GitHub' }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${prefix}-operations'
  location: 'global'
  properties: { groupShortName: 'nexoraops', enabled: true, emailReceivers: [ { name: 'operations', emailAddress: alertEmail, useCommonAlertSchema: true } ] }
}
module alerts 'alerts.bicep' = if (deployWorkloads) {
  name: 'production-alerts'
  params: { prefix: prefix, containerAppId: api!.id, postgresId: postgres.id, actionGroupId: actionGroup.id }
}

output apiFqdn string = deployWorkloads ? api!.properties.configuration.ingress.fqdn : ''
output staticWebAppHostname string = staticWeb.properties.defaultHostname
output registryName string = registry.name
output keyVaultName string = vault.name
output migrationJobName string = deployWorkloads ? migrationJob!.name : ''

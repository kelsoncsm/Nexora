param prefix string
param containerAppId string
param postgresId string
param actionGroupId string

resource apiUnavailable 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-api-no-replicas'
  location: 'global'
  properties: {
    description: 'No running API replicas. Investigate Container Apps revision and readiness.'
    severity: 0
    enabled: true
    scopes: [ containerAppId ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: { 'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria', allOf: [ { name: 'NoReplicas', metricName: 'Replicas', metricNamespace: 'Microsoft.App/containerApps', operator: 'LessThan', threshold: 1, timeAggregation: 'Average', criterionType: 'StaticThresholdCriterion' } ] }
    actions: [ { actionGroupId: actionGroupId } ]
  }
}

resource postgresCpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-postgres-cpu'
  location: 'global'
  properties: {
    description: 'PostgreSQL CPU is above 90% for 15 minutes.'
    severity: 2
    enabled: true
    scopes: [ postgresId ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: { 'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria', allOf: [ { name: 'HighCpu', metricName: 'cpu_percent', metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers', operator: 'GreaterThan', threshold: 90, timeAggregation: 'Average', criterionType: 'StaticThresholdCriterion' } ] }
    actions: [ { actionGroupId: actionGroupId } ]
  }
}

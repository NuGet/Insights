param storageAccountName string
param appInsightsName string
param websiteName string

param actionGroupName string
param alertEmail string
param actionGroupShortName string
param alertPrefix string

resource storageAccount 'Microsoft.Storage/storageAccounts@2019-06-01' existing = {
  name: storageAccountName
}

resource appInsights 'Microsoft.Insights/components@2015-05-01' existing = {
  name: appInsightsName
}

resource website 'Microsoft.Web/sites@2020-09-01' existing = {
  name: websiteName
}

resource actionGroup 'Microsoft.Insights/actionGroups@2019-06-01' = {
  name: actionGroupName
  location: 'Global'
  properties: empty(alertEmail) ? {
    groupShortName: actionGroupShortName
    enabled: true
  } : {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: [
      {
        name: 'recipient_-EmailAction-'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource expandDLQAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${alertPrefix}NuGet.Insights dead-letter queue "expand-poison" is not empty'
  location: 'global'
  properties: {
    description: 'The Azure Queue Storage queue "expand-poison" for NuGet.Insights deployed to resource group "${resourceGroup().name}" has at least one message in it. This may be blocking the NuGet.Insights workflow or other regular operations from continuing. Check the "expand-poison" queue in the "${storageAccount.name}" storage account to see the message or look at logs in the "${appInsights.name}" Application Insights to investigate.'
    severity: 3
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          threshold: 0
          name: 'ExpandDLQMax'
          metricNamespace: 'Azure.ApplicationInsights'
          metricName: 'StorageQueueSize.Expand.Poison'
          operator: 'GreaterThan'
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
          skipMetricValidation: true
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource workDLQAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${alertPrefix}NuGet.Insights dead-letter queue "work-poison" is not empty'
  location: 'global'
  properties: {
    description: 'The Azure Queue Storage queue "work-poison" for NuGet.Insights deployed to resource group "${resourceGroup().name}" has at least one message in it. This may be blocking the NuGet.Insights workflow or other regular operations from continuing. Check the "work-poison" queue in the "${storageAccount.name}" storage account to see the message or look at logs in the "${appInsights.name}" Application Insights to investigate.'
    severity: 3
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          threshold: 0
          name: 'WorkDLQMax'
          metricNamespace: 'Azure.ApplicationInsights'
          metricName: 'StorageQueueSize.Work.Poison'
          operator: 'GreaterThan'
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
          skipMetricValidation: true
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource recentWorkflowAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${alertPrefix}NuGet.Insights workflow has not completed in the past 48 hours'
  location: 'global'
  properties: {
    description: 'The NuGet.Insights workflow (catalog scan, Kusto import, etc) for NuGet.Insights deployed to resource group "${resourceGroup().name}" has not completed for at least the past 48 hours. It should complete every 24 hours. Check https://${website.properties.defaultHostName}/admin and logs in the "${appInsights.name}" Application Insights to investigate.'
    severity: 3
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          threshold: 48
          name: 'HoursSinceWorkflowCompletedMax'
          metricNamespace: 'Azure.ApplicationInsights'
          metricName: 'SinceLastWorkflowCompletedHours'
          operator: 'GreaterThan'
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
          skipMetricValidation: true
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

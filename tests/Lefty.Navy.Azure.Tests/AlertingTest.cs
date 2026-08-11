using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class AlertingTest
{
    private const string GroupId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/actionGroups/ag-critical";
    private const string ComponentId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/components/appi-one";

    private static readonly ResourceMapper Mapper = new( NullLogger.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger.Instance );


    /// <summary />
    private static T Map<T>( string json )
        where T : AzResource
    {
        return Assert.IsType<T>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    /// <remarks>
    /// Azure reports eleven arrays of receivers, one per kind, and all but a
    /// couple are empty on any real group. They land in one list.
    /// </remarks>
    [Fact]
    public void ActionGroup_ReceiversAreFlattened()
    {
        var group = Map<AzActionGroup>( GroupJson );

        Assert.Equal( "core-crit", group.GroupShortName );
        Assert.True( group.Enabled );
        Assert.Equal( 3, group.Receivers.Count );

        var email = group.Receivers[ 0 ];

        Assert.Equal( "Email", email.Kind );
        Assert.Equal( "Operations", email.Name );
        Assert.Equal( "operations@example.org", email.Target );
        Assert.Equal( "Enabled", email.Status );
        Assert.True( email.UseCommonAlertSchema );

        var webhook = group.Receivers[ 1 ];

        Assert.Equal( "Webhook", webhook.Kind );
        Assert.Equal( "https://hooks.example.org/alerts", webhook.Target );
        Assert.Null( webhook.Status );

        var role = group.Receivers[ 2 ];

        Assert.Equal( "ArmRole", role.Kind );
        Assert.Equal( "8e3af657-a8ff-443c-a75c-2fe8c4bcb635", role.Target );
    }


    /// <summary />
    [Fact]
    public void ActivityLogAlertRule_IsFullyMapped()
    {
        var rule = Map<AzActivityLogAlertRule>( ActivityLogJson );

        Assert.True( rule.Enabled );
        Assert.Equal( "Activity Log alert for route table writes.", rule.Description );
        Assert.Equal( [ "/subscriptions/s" ], rule.Scopes );
        Assert.Equal( [ GroupId ], rule.ActionGroupIds );

        Assert.Equal( 2, rule.Conditions.Count );
        Assert.Equal( "category", rule.Conditions[ 0 ].Field );
        Assert.Equal( "Administrative", rule.Conditions[ 0 ].EqualTo );
        Assert.Empty( rule.Conditions[ 0 ].AnyOf );
    }


    /// <summary />
    /// <remarks>
    /// A condition may stand for a set of alternatives instead of naming a
    /// field, and may accept any of several values instead of just one.
    /// </remarks>
    [Fact]
    public void ActivityLogAlertRule_NestedConditionIsMapped()
    {
        var rule = Map<AzActivityLogAlertRule>( ActivityLogJson );

        var alternatives = rule.Conditions[ 1 ];

        Assert.Null( alternatives.Field );
        Assert.Equal( 2, alternatives.AnyOf.Count );
        Assert.Equal( "operationName", alternatives.AnyOf[ 0 ].Field );
        Assert.Equal( "Microsoft.Network/routeTables/write", alternatives.AnyOf[ 0 ].EqualTo );
        Assert.Equal( [ "Warning", "Error" ], alternatives.AnyOf[ 1 ].ContainsAny );
        Assert.Null( alternatives.AnyOf[ 1 ].EqualTo );
    }


    /// <summary />
    [Fact]
    public void MetricAlertRule_IsFullyMapped()
    {
        var rule = Map<AzMetricAlertRule>( MetricAlertJson );

        Assert.Equal( "Alert when the account fills up", rule.Description );
        Assert.Equal( 2, rule.Severity );
        Assert.True( rule.Enabled );
        Assert.True( rule.AutoMitigate );
        Assert.Equal( "PT1H", rule.EvaluationFrequency );
        Assert.Equal( "PT6H", rule.WindowSize );
        Assert.Equal( "MultipleResourceMultipleMetricCriteria", rule.CriteriaType );
        Assert.Equal( [ GroupId ], rule.ActionGroupIds );

        var criterion = Assert.Single( rule.Criteria );

        Assert.Equal( "Metric1", criterion.Name );
        Assert.Equal( "StaticThresholdCriterion", criterion.CriterionType );
        Assert.Equal( "UsedCapacity", criterion.MetricName );
        Assert.Equal( "Microsoft.Storage/storageAccounts", criterion.MetricNamespace );
        Assert.Equal( "GreaterThan", criterion.Operator );
        Assert.Equal( "Average", criterion.TimeAggregation );
        Assert.False( criterion.SkipMetricValidation );
    }


    /// <summary />
    /// <remarks>
    /// A threshold is reported as a fractional number, and a capacity threshold
    /// is far past the range of a 32 bit integer.
    /// </remarks>
    [Fact]
    public void MetricAlertRule_ThresholdKeepsItsMagnitude()
    {
        var rule = Map<AzMetricAlertRule>( MetricAlertJson );

        Assert.Equal( 4398046511104.0, Assert.Single( rule.Criteria ).Threshold );
    }


    /// <summary />
    /// <remarks>
    /// A dynamic criterion has Azure work the threshold out for itself, and
    /// states a sensitivity and a run of failures in its place.
    /// </remarks>
    [Fact]
    public void MetricAlertRule_DynamicCriterionIsMapped()
    {
        var rule = Map<AzMetricAlertRule>( DynamicMetricAlertJson );

        var criterion = Assert.Single( rule.Criteria );

        Assert.Equal( "DynamicThresholdCriterion", criterion.CriterionType );
        Assert.Equal( "Medium", criterion.AlertSensitivity );
        Assert.Equal( 3, criterion.FailingPeriodsToAlert );
        Assert.Equal( 4, criterion.FailingPeriodsWindow );

        var dimension = Assert.Single( criterion.Dimensions );

        Assert.Equal( "ApiName", dimension.Name );
        Assert.Equal( "Include", dimension.Operator );
        Assert.Equal( [ "*" ], dimension.Values );
    }


    /// <summary />
    /// <remarks>
    /// A web test rule states its condition on the criteria itself rather than
    /// in a list of criteria, and so has none.
    /// </remarks>
    [Fact]
    public void MetricAlertRule_WebTestCriteriaIsMapped()
    {
        var rule = Map<AzMetricAlertRule>( WebTestAlertJson );

        Assert.Equal( "WebtestLocationAvailabilityCriteria", rule.CriteriaType );
        Assert.Equal( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/webtests/wt-home", rule.WebTestId );
        Assert.Equal( ComponentId, rule.ComponentId );
        Assert.Equal( 3, rule.FailedLocationCount );
        Assert.Empty( rule.Criteria );
    }


    /// <summary />
    [Fact]
    public void SmartDetectorAlertRule_IsFullyMapped()
    {
        var rule = Map<AzSmartDetectorAlertRule>( SmartDetectorJson );

        Assert.Equal( "Enabled", rule.State );
        Assert.Equal( "Sev3", rule.Severity );
        Assert.Equal( "PT1M", rule.Frequency );
        Assert.Equal( "FailureAnomaliesDetector", rule.DetectorId );
        Assert.Equal( "Failure Anomalies", rule.DetectorName );
        Assert.Equal( [ "ApplicationInsights" ], rule.DetectorSupportedResourceTypes );
        Assert.Equal( [ ComponentId ], rule.Scopes );
        Assert.Equal( [ GroupId ], rule.ActionGroupIds );
        Assert.Null( rule.ThrottlingDuration );
        Assert.Null( rule.CustomEmailSubject );
    }


    /// <summary />
    /// <remarks>
    /// Azure returns several paragraphs of HTML describing the detector, the
    /// same paragraphs under every rule which runs it. Keeping them would make
    /// the inventory mostly marketing copy.
    /// </remarks>
    [Fact]
    public void SmartDetectorAlertRule_DetectorDescriptionIsNotMapped()
    {
        var rule = Map<AzSmartDetectorAlertRule>( SmartDetectorJson );

        var json = JsonSerializer.Serialize<AzResource>( rule );

        Assert.DoesNotContain( "ext-smartDetector-link", json );
        Assert.Contains( "FailureAnomaliesDetector", json );
    }


    /// <summary />
    [Fact]
    public void ApplicationInsights_IsFullyMapped()
    {
        var component = Map<AzApplicationInsights>( ComponentJson );

        Assert.Equal( "web", component.Kind );
        Assert.Equal( "web", component.ApplicationType );
        Assert.Equal( "a90e22d0-67ef-47dc-be67-3cd3cf1e4cf3", component.AppId );
        Assert.Equal( "Succeeded", component.ProvisioningState );
        Assert.Equal( 2026, component.CreationDate!.Value.Year );

        Assert.Equal( "LogAnalytics", component.IngestionMode );
        Assert.Equal( 90, component.RetentionInDays );
        Assert.Equal( 100.0, component.SamplingPercentage );
        Assert.False( component.DisableIpMasking );
        Assert.Equal( "Disabled", component.PublicNetworkAccessForIngestion );
        Assert.Equal( "Disabled", component.PublicNetworkAccessForQuery );

        Assert.EndsWith( "/workspaces/log-one", component.WorkspaceResourceId );
        Assert.Single( component.PrivateLinkScopedResourceIds );
    }


    /// <summary />
    /// <remarks>
    /// The instrumentation key is an ingestion credential, and the connection
    /// string contains it. Neither belongs in a file which gets passed around.
    /// </remarks>
    [Fact]
    public void ApplicationInsights_CredentialsAreNotMapped()
    {
        var component = Map<AzApplicationInsights>( ComponentJson );

        var json = JsonSerializer.Serialize<AzResource>( component );

        Assert.DoesNotContain( "c1bfb926-9380-4704-803a-823c6b43b0eb", json );
        Assert.DoesNotContain( "InstrumentationKey", json );
    }


    /// <summary />
    /// <remarks>
    /// The one relationship all three kinds of alert rule share.
    /// </remarks>
    [Fact]
    public void AlertRules_ActionGroupsAreResolved()
    {
        var group = Map<AzActionGroup>( GroupJson );
        var activity = Map<AzActivityLogAlertRule>( ActivityLogJson );
        var metric = Map<AzMetricAlertRule>( MetricAlertJson );
        var detector = Map<AzSmartDetectorAlertRule>( SmartDetectorJson );

        Linker.Link( [ group, activity, metric, detector ] );

        Assert.Same( group, Assert.Single( activity.ActionGroups ) );
        Assert.Same( group, Assert.Single( metric.ActionGroups ) );
        Assert.Same( group, Assert.Single( detector.ActionGroups ) );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ group, activity, metric, detector ] );

        Assert.Contains( "ag-critical", json );
    }


    /// <summary />
    /// <remarks>
    /// A rule whose group sits in a subscription which was not read keeps the
    /// identifier and resolves to nothing.
    /// </remarks>
    [Fact]
    public void AlertRule_WithoutItsActionGroup_IsLeftEmpty()
    {
        var metric = Map<AzMetricAlertRule>( MetricAlertJson );

        Linker.Link( [ metric ] );

        Assert.Equal( [ GroupId ], metric.ActionGroupIds );
        Assert.Empty( metric.ActionGroups );
    }


    /// <summary />
    /// <remarks>
    /// What a rule watches is left as an identifier, or a subscription holding
    /// a hundred rules would hold a hundred copies of what they watch.
    /// </remarks>
    [Fact]
    public void MetricAlertRule_ScopesAreNotResolved()
    {
        var metric = Map<AzMetricAlertRule>( MetricAlertJson );
        var account = Map<AzStorageAccount>( AccountJson );

        Linker.Link( [ metric, account ] );

        Assert.Equal( [ account.Id ], metric.Scopes );

        var json = JsonSerializer.Serialize<AzResource>( metric );

        Assert.DoesNotContain( "AzStorageAccount", json );
    }


    private const string GroupJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/actionGroups/ag-critical",
          "name": "ag-critical",
          "type": "microsoft.insights/actiongroups",
          "location": "global",
          "properties": {
            "armRoleReceivers": [
              { "name": "Owners", "roleId": "8e3af657-a8ff-443c-a75c-2fe8c4bcb635", "useCommonAlertSchema": true }
            ],
            "automationRunbookReceivers": [],
            "azureAppPushReceivers": [],
            "azureFunctionReceivers": [],
            "emailReceivers": [
              { "emailAddress": "operations@example.org", "name": "Operations", "status": "Enabled", "useCommonAlertSchema": true }
            ],
            "enabled": true,
            "eventHubReceivers": [],
            "groupShortName": "core-crit",
            "itsmReceivers": [],
            "logicAppReceivers": [],
            "smsReceivers": [],
            "voiceReceivers": [],
            "webhookReceivers": [
              {
                "identifierUri": null,
                "name": "Teams-Channel",
                "objectId": null,
                "serviceUri": "https://hooks.example.org/alerts",
                "tenantId": null,
                "useAadAuth": false,
                "useCommonAlertSchema": true
              }
            ]
          }
        }
        """;

    private const string ActivityLogJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/activityLogAlerts/alert-route-table-write",
          "name": "alert-route-table-write",
          "type": "microsoft.insights/activitylogalerts",
          "location": "global",
          "properties": {
            "actions": {
              "actionGroups": [
                {
                  "actionGroupId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/actionGroups/ag-critical",
                  "webhookProperties": {}
                }
              ]
            },
            "condition": {
              "allOf": [
                { "equals": "Administrative", "field": "category" },
                {
                  "anyOf": [
                    { "equals": "Microsoft.Network/routeTables/write", "field": "operationName" },
                    { "field": "level", "containsAny": [ "Warning", "Error" ] }
                  ]
                }
              ]
            },
            "description": "Activity Log alert for route table writes.",
            "enabled": true,
            "scopes": [ "/subscriptions/s" ]
          }
        }
        """;

    private const string MetricAlertJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/metricalerts/alert-capacity-high",
          "name": "alert-capacity-high",
          "type": "microsoft.insights/metricalerts",
          "location": "global",
          "properties": {
            "actions": [
              {
                "actionGroupId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/actionGroups/ag-critical",
                "webHookProperties": {}
              }
            ],
            "autoMitigate": true,
            "criteria": {
              "allOf": [
                {
                  "criterionType": "StaticThresholdCriterion",
                  "metricName": "UsedCapacity",
                  "metricNamespace": "Microsoft.Storage/storageAccounts",
                  "name": "Metric1",
                  "operator": "GreaterThan",
                  "skipMetricValidation": false,
                  "threshold": 4398046511104.0,
                  "timeAggregation": "Average"
                }
              ],
              "odata.type": "Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria"
            },
            "description": "Alert when the account fills up",
            "enabled": true,
            "evaluationFrequency": "PT1H",
            "scopes": [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone" ],
            "severity": 2,
            "targetResourceRegion": "",
            "targetResourceType": "",
            "windowSize": "PT6H"
          }
        }
        """;

    private const string DynamicMetricAlertJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/metricalerts/alert-requests-unusual",
          "name": "alert-requests-unusual",
          "type": "microsoft.insights/metricalerts",
          "location": "global",
          "properties": {
            "actions": [],
            "autoMitigate": true,
            "criteria": {
              "allOf": [
                {
                  "alertSensitivity": "Medium",
                  "criterionType": "DynamicThresholdCriterion",
                  "dimensions": [ { "name": "ApiName", "operator": "Include", "values": [ "*" ] } ],
                  "failingPeriods": { "minFailingPeriodsToAlert": 3, "numberOfEvaluationPeriods": 4 },
                  "metricName": "Transactions",
                  "metricNamespace": "Microsoft.Storage/storageAccounts",
                  "name": "Metric1",
                  "operator": "GreaterOrLessThan",
                  "skipMetricValidation": false,
                  "timeAggregation": "Total"
                }
              ],
              "odata.type": "Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria"
            },
            "enabled": true,
            "evaluationFrequency": "PT5M",
            "scopes": [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone" ],
            "severity": 3,
            "windowSize": "PT15M"
          }
        }
        """;

    private const string WebTestAlertJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/metricalerts/alert-home-unavailable",
          "name": "alert-home-unavailable",
          "type": "microsoft.insights/metricalerts",
          "location": "global",
          "properties": {
            "actions": [],
            "criteria": {
              "componentId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/components/appi-one",
              "failedLocationCount": 3,
              "odata.type": "Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria",
              "webTestId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/webtests/wt-home"
            },
            "enabled": true,
            "evaluationFrequency": "PT1M",
            "scopes": [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/webtests/wt-home" ],
            "severity": 1,
            "windowSize": "PT5M"
          }
        }
        """;

    private const string SmartDetectorJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/microsoft.alertsmanagement/smartDetectorAlertRules/Failure Anomalies - appi-one",
          "name": "Failure Anomalies - appi-one",
          "type": "microsoft.alertsmanagement/smartdetectoralertrules",
          "location": "global",
          "properties": {
            "actionGroups": {
              "customEmailSubject": null,
              "customWebhookPayload": null,
              "groupIds": [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/actionGroups/ag-critical" ]
            },
            "description": "Failure Anomalies notifies you of an unusual rise in the rate of failed HTTP requests.",
            "detector": {
              "description": "Detects if your application experiences an abnormal rise in failures.<br><br><a class=\"ext-smartDetector-link\" href=\"https://learn.microsoft.com/\">Learn more</a>",
              "id": "FailureAnomaliesDetector",
              "name": "Failure Anomalies",
              "parameterDefinitions": [],
              "parameters": null,
              "supportedCadences": [ 1 ],
              "supportedResourceTypes": [ "ApplicationInsights" ]
            },
            "frequency": "PT1M",
            "scope": [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/components/appi-one" ],
            "severity": "Sev3",
            "state": "Enabled",
            "throttling": null
          }
        }
        """;

    private const string ComponentJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Insights/components/appi-one",
          "name": "appi-one",
          "type": "microsoft.insights/components",
          "location": "westeurope",
          "kind": "web",
          "properties": {
            "AppId": "a90e22d0-67ef-47dc-be67-3cd3cf1e4cf3",
            "ApplicationId": "appi-one",
            "Application_Type": "web",
            "ConnectionString": "InstrumentationKey=c1bfb926-9380-4704-803a-823c6b43b0eb;IngestionEndpoint=https://westeurope-0.in.applicationinsights.azure.com/",
            "CreationDate": "2026-01-28T18:45:31.2065151Z",
            "DisableIpMasking": false,
            "DisableLocalAuth": false,
            "IngestionMode": "LogAnalytics",
            "InstrumentationKey": "c1bfb926-9380-4704-803a-823c6b43b0eb",
            "Name": "appi-one",
            "PrivateLinkScopedResources": [
              {
                "ResourceId": "/subscriptions/s/resourceGroups/rg/providers/microsoft.insights/privatelinkscopes/ampls-one/scopedresources/appi-one",
                "ScopeId": "b49a4de9-4480-40e9-96ef-37c403695e8c"
              }
            ],
            "Retention": "P90D",
            "RetentionInDays": 90,
            "SamplingPercentage": 100.0,
            "Ver": "v2",
            "WorkspaceResourceId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.OperationalInsights/workspaces/log-one",
            "provisioningState": "Succeeded",
            "publicNetworkAccessForIngestion": "Disabled",
            "publicNetworkAccessForQuery": "Disabled"
          }
        }
        """;

    private const string AccountJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone",
          "name": "stone",
          "type": "microsoft.storage/storageaccounts",
          "location": "westeurope",
          "kind": "StorageV2",
          "sku": { "name": "Standard_LRS", "tier": "Standard" },
          "properties": { "accessTier": "Hot", "provisioningState": "Succeeded" }
        }
        """;
}

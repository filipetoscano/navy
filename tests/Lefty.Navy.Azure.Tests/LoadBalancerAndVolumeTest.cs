using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class LoadBalancerAndVolumeTest
{
    private const string BalancerId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one";
    private const string MemberId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachineScaleSets/vmss-one/virtualMachines/7/networkInterfaces/vmss-one/ipConfigurations/ipconfig1";
    private const string AccountId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-one";
    private const string PoolId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-one/capacityPools/pool-ultra";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-data";

    private static readonly ResourceMapper Mapper = new( NullLogger<ResourceMapper>.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger<ResourceLinker>.Instance );


    /// <summary />
    private static T Map<T>( string json )
        where T : AzResource
    {
        return Assert.IsType<T>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    [Fact]
    public void LoadBalancer_IsFullyMapped()
    {
        var balancer = Map<AzLoadBalancer>( BalancerJson );

        Assert.Equal( "Standard", balancer.Sku );
        Assert.Equal( "Regional", balancer.SkuTier );
        Assert.Equal( "Succeeded", balancer.ProvisioningState );

        var frontend = Assert.Single( balancer.FrontendIPConfigurations );

        Assert.Equal( "frontend-internal", frontend.Name );
        Assert.Equal( "Microsoft.Network/loadBalancers/frontendIPConfigurations", frontend.Type );
        Assert.Equal( "10.200.5.209", frontend.PrivateIPAddress );
        Assert.Equal( "Static", frontend.PrivateIPAllocationMethod );
        Assert.Equal( SubnetId, frontend.SubnetId );
        Assert.Null( frontend.PublicIPAddressId );
        Assert.Equal( [ "2" ], frontend.Zones );

        var probe = Assert.Single( balancer.Probes );

        Assert.Equal( "Tcp", probe.Protocol );
        Assert.Equal( 31103, probe.Port );
        Assert.Equal( 5, probe.IntervalInSeconds );
        Assert.Equal( 2, probe.ProbeThreshold );
    }


    /// <summary />
    [Fact]
    public void LoadBalancer_RulesAreMapped()
    {
        var balancer = Map<AzLoadBalancer>( BalancerJson );

        var rule = Assert.Single( balancer.LoadBalancingRules );

        Assert.Equal( "Tcp", rule.Protocol );
        Assert.Equal( 80, rule.FrontendPort );
        Assert.Equal( 80, rule.BackendPort );
        Assert.Equal( "Default", rule.LoadDistribution );
        Assert.Equal( 4, rule.IdleTimeoutInMinutes );
        Assert.True( rule.EnableFloatingIP );
        Assert.True( rule.EnableTcpReset );
        Assert.True( rule.DisableOutboundSnat );
        Assert.EndsWith( "/backendAddressPools/kubernetes", rule.BackendPoolId );
        Assert.EndsWith( "/probes/tcp-80", rule.ProbeId );

        var nat = Assert.Single( balancer.InboundNatRules );

        Assert.Equal( 50000, nat.FrontendPort );
        Assert.Equal( 22, nat.BackendPort );

        var outbound = Assert.Single( balancer.OutboundRules );

        Assert.Equal( "All", outbound.Protocol );
        Assert.Equal( 1024, outbound.AllocatedOutboundPorts );
        Assert.Single( outbound.FrontendIPConfigurationIds );
    }


    /// <summary />
    /// <remarks>
    /// The members of a pool are the instance interfaces behind the balancer.
    /// There are commonly hundreds of them, they change as the scale set does,
    /// and they would be most of the inventory if they were kept.
    /// </remarks>
    [Fact]
    public void LoadBalancer_BackendPoolMembersAreCountedOnly()
    {
        var balancer = Map<AzLoadBalancer>( BalancerJson );

        var pool = Assert.Single( balancer.BackendPools );

        Assert.Equal( "kubernetes", pool.Name );
        Assert.Equal( "Succeeded", pool.ProvisioningState );
        Assert.Equal( 2, pool.MemberCount );

        var json = JsonSerializer.Serialize<AzResource>( balancer );

        Assert.DoesNotContain( MemberId, json );
    }


    /// <summary />
    [Fact]
    public void LoadBalancer_FrontendSubnetIsResolved()
    {
        var balancer = Map<AzLoadBalancer>( BalancerJson );
        var network = Network();

        Linker.Link( [ balancer, network ] );

        Assert.Same( network.Subnets[ 0 ], balancer.FrontendIPConfigurations[ 0 ].Subnet );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ balancer, network ] );

        Assert.Contains( "lb-one", json );
    }


    /// <summary />
    [Fact]
    public void Volume_IsFullyMapped()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Assert.Equal( PoolId, volume.CapacityPoolId );
        Assert.Equal( "Succeeded", volume.ProvisioningState );
        Assert.Equal( "pvc-one", volume.CreationToken );
        Assert.Equal( "af0d98da-f1a2-00f1-0b5f-f60c4af1d091", volume.FileSystemId );

        Assert.Equal( "Ultra", volume.ServiceLevel );
        Assert.Equal( 6.25, volume.ThroughputMibps );
        Assert.False( volume.CoolAccess );

        Assert.Equal( [ "NFSv4.1" ], volume.ProtocolTypes );
        Assert.Equal( "Unix", volume.SecurityStyle );
        Assert.Equal( "0777", volume.UnixPermissions );
        Assert.False( volume.KerberosEnabled );
        Assert.False( volume.LdapEnabled );
        Assert.False( volume.SnapshotDirectoryVisible );
        Assert.Equal( "Microsoft.NetApp", volume.EncryptionKeySource );

        Assert.Equal( SubnetId, volume.SubnetId );
        Assert.Equal( "Basic", volume.NetworkFeatures );
        Assert.Equal( [ "10.200.5.148" ], volume.MountTargetIPAddresses );
    }


    /// <summary />
    /// <remarks>
    /// A volume is provisioned in bytes, and the smallest one Azure allows is
    /// already past the range of a 32 bit integer.
    /// </remarks>
    [Fact]
    public void Volume_SizeSurvivesLargeValues()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Assert.Equal( 53687091200L, volume.UsageThreshold );
        Assert.Equal( 1556473L, volume.MaximumNumberOfFiles );
    }


    /// <summary />
    [Fact]
    public void Volume_ExportRulesAreMapped()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );

        var rule = Assert.Single( volume.ExportRules );

        Assert.Equal( 1, rule.RuleIndex );
        Assert.Equal( "0.0.0.0/0", rule.AllowedClients );
        Assert.False( rule.Nfsv3 );
        Assert.True( rule.Nfsv41 );
        Assert.False( rule.Cifs );
        Assert.False( rule.UnixReadOnly );
        Assert.True( rule.UnixReadWrite );
        Assert.True( rule.HasRootAccess );
        Assert.Equal( "Restricted", rule.ChownMode );
    }


    /// <summary />
    [Fact]
    public void Volume_SubnetIsResolved()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );
        var network = Network();

        Linker.Link( [ volume, network ] );

        Assert.Same( network.Subnets[ 0 ], volume.Subnet );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ volume, network ] );

        Assert.Contains( "AzNetAppVolume", json );
    }


    /// <summary />
    /// <remarks>
    /// Resource Graph reports the whole path in the name of a volume, because
    /// it is a grandchild of the NetApp account rather than a child.
    /// </remarks>
    [Fact]
    public void Volume_KeepsItsCompoundName()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Assert.Equal( "anf-one/pool-ultra/pvc-one", volume.Name );
    }


    /// <summary />
    /// <remarks>
    /// The two ancestors of a volume are stated nowhere but in its identifier.
    /// </remarks>
    [Fact]
    public void Volume_NamesItsPoolAndAccount()
    {
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Assert.Equal( PoolId, volume.CapacityPoolId );
        Assert.Equal( AccountId, volume.NetAppAccountId );
    }


    /// <summary />
    /// <remarks>
    /// An account which serves NFS without LDAP joins no directory and uses a
    /// platform-managed key, which is the ordinary case and reports almost
    /// nothing.
    /// </remarks>
    [Fact]
    public void NetAppAccount_IsFullyMapped()
    {
        var account = Map<AzNetAppAccount>( AccountJson );

        Assert.Equal( "anf-one", account.Name );
        Assert.Equal( "Succeeded", account.ProvisioningState );
        Assert.Equal( "Enabled", account.MultiAdStatus );
        Assert.Equal( "Microsoft.NetApp", account.EncryptionKeySource );
        Assert.Null( account.EncryptionKeyVaultId );
        Assert.Null( account.EncryptionIdentityId );
        Assert.Empty( account.ActiveDirectories );
        Assert.False( account.DisableShowmount );
    }


    /// <summary />
    [Fact]
    public void NetAppAccount_WithCustomerKey_ResolvesTheVault()
    {
        var account = Map<AzNetAppAccount>( EncryptedAccountJson );
        var vault = Map<AzKeyVault>( VaultJson );
        var identity = Map<AzManagedIdentity>( IdentityJson );

        Assert.Equal( "Microsoft.KeyVault", account.EncryptionKeySource );
        Assert.Equal( "cmk-netapp", account.EncryptionKeyName );
        Assert.Equal( "https://kv-one.vault.azure.net", account.EncryptionKeyVaultUri );

        Linker.Link( [ account, vault, identity ] );

        Assert.Same( vault, account.EncryptionKeyVault );
        Assert.Same( identity, account.EncryptionIdentity );
    }


    /// <summary />
    [Fact]
    public void NetAppAccount_DirectoryIsMapped()
    {
        var account = Map<AzNetAppAccount>( EncryptedAccountJson );

        var directory = Assert.Single( account.ActiveDirectories );

        Assert.Equal( "corp.example.org", directory.Domain );
        Assert.Equal( "anf-join", directory.Username );
        Assert.Equal( "10.200.0.4,10.200.0.5", directory.Dns );
        Assert.Equal( "ANF-SMB", directory.SmbServerName );
        Assert.Equal( "OU=Storage,DC=corp,DC=example,DC=org", directory.OrganizationalUnit );
        Assert.Equal( "InUse", directory.Status );
        Assert.True( directory.AesEncryption );
        Assert.True( directory.LdapSigning );
        Assert.False( directory.LdapOverTls );
        Assert.False( directory.AllowLocalNfsUsersWithLdap );
    }


    /// <summary />
    [Fact]
    public void CapacityPool_IsFullyMapped()
    {
        var pool = Map<AzNetAppCapacityPool>( PoolJson );

        Assert.Equal( "anf-one/pool-ultra", pool.Name );
        Assert.Equal( AccountId, pool.NetAppAccountId );
        Assert.Equal( "Succeeded", pool.ProvisioningState );
        Assert.Equal( "8f9780cd-a12c-3983-fae9-d68e872a59bf", pool.PoolId );

        Assert.Equal( "Ultra", pool.ServiceLevel );
        Assert.Equal( 5497558138880L, pool.Size );
        Assert.Equal( "Auto", pool.QosType );
        Assert.Equal( 640.0, pool.TotalThroughputMibps );
        Assert.Equal( 301.5, pool.UtilizedThroughputMibps );
        Assert.False( pool.CoolAccess );
        Assert.Equal( "Single", pool.EncryptionType );
    }


    /// <summary />
    /// <remarks>
    /// Azure reports the account, the pool and the volume as three unrelated
    /// resources, each naming its parent only within its own identifier. The
    /// linker puts the hierarchy back together from the children.
    /// </remarks>
    [Fact]
    public void NetApp_HierarchyIsAssembled()
    {
        var account = Map<AzNetAppAccount>( AccountJson );
        var pool = Map<AzNetAppCapacityPool>( PoolJson );
        var volume = Map<AzNetAppVolume>( VolumeJson );
        var other = Map<AzNetAppVolume>( VolumeJson.Replace( "pvc-one", "pvc-two" ) );

        Linker.Link( [ account, pool, volume, other ] );

        Assert.Same( pool, Assert.Single( account.CapacityPools ) );
        Assert.Equal( 2, pool.Volumes.Count );
        Assert.Same( volume, pool.Volumes[ 0 ] );

        // Each child names its parent, and must not resolve it.
        var json = JsonSerializer.Serialize<List<AzResource>>( [ account, pool, volume, other ] );

        Assert.Contains( "anf-one/pool-ultra", json );
    }


    /// <summary />
    /// <remarks>
    /// A volume whose pool is missing hangs off nothing, but is kept rather
    /// than being discarded for having nowhere to attach.
    /// </remarks>
    [Fact]
    public void Volume_WithoutItsPool_IsStillMapped()
    {
        var account = Map<AzNetAppAccount>( AccountJson );
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Linker.Link( [ account, volume ] );

        Assert.Equal( PoolId, volume.CapacityPoolId );
        Assert.Equal( AccountId, volume.NetAppAccountId );
        Assert.Equal( "pvc-one", volume.CreationToken );
        Assert.Empty( account.CapacityPools );
    }


    /// <summary />
    /// <remarks>
    /// A volume reaches its account through the pool, so the account must not
    /// hold it a second time.
    /// </remarks>
    [Fact]
    public void NetAppAccount_DoesNotHoldVolumesDirectly()
    {
        var account = Map<AzNetAppAccount>( AccountJson );
        var pool = Map<AzNetAppCapacityPool>( PoolJson );
        var volume = Map<AzNetAppVolume>( VolumeJson );

        Linker.Link( [ account, pool, volume ] );

        var json = JsonSerializer.Serialize<AzResource>( account );

        Assert.Equal( 1, Occurrences( json, "\"CreationToken\":\"pvc-one\"" ) );
    }


    /// <summary />
    private static int Occurrences( string text, string value )
    {
        var count = 0;
        var at = text.IndexOf( value, StringComparison.Ordinal );

        while ( at >= 0 )
        {
            count++;
            at = text.IndexOf( value, at + value.Length, StringComparison.Ordinal );
        }

        return count;
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Activator.CreateInstance<AzVirtualNetwork>();

        network.Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one";
        network.Name = "vnet-one";
        network.Type = "Microsoft.Network/virtualNetworks";
        network.Location = "westeurope";
        network.AddressPrefixes = [ "10.200.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-data",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.200.5.0/24",
            },
        ];

        return network;
    }


    private const string BalancerJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one",
          "name": "lb-one",
          "type": "microsoft.network/loadbalancers",
          "location": "westeurope",
          "sku": { "name": "Standard", "tier": "Regional" },
          "properties": {
            "provisioningState": "Succeeded",
            "resourceGuid": "de09463c-7f32-432a-a9d7-54bb8e9cffaa",
            "backendAddressPools": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/backendAddressPools/kubernetes",
                "name": "kubernetes",
                "type": "Microsoft.Network/loadBalancers/backendAddressPools",
                "properties": {
                  "backendIPConfigurations": [
                    { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachineScaleSets/vmss-one/virtualMachines/7/networkInterfaces/vmss-one/ipConfigurations/ipconfig1" },
                    { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachineScaleSets/vmss-one/virtualMachines/8/networkInterfaces/vmss-one/ipConfigurations/ipconfig1" }
                  ],
                  "provisioningState": "Succeeded"
                }
              }
            ],
            "frontendIPConfigurations": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/frontendIPConfigurations/frontend-internal",
                "name": "frontend-internal",
                "type": "Microsoft.Network/loadBalancers/frontendIPConfigurations",
                "zones": [ "2" ],
                "properties": {
                  "privateIPAddress": "10.200.5.209",
                  "privateIPAddressVersion": "IPv4",
                  "privateIPAllocationMethod": "Static",
                  "provisioningState": "Succeeded",
                  "subnet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-data" }
                }
              }
            ],
            "inboundNatPools": [],
            "inboundNatRules": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/inboundNatRules/ssh",
                "name": "ssh",
                "type": "Microsoft.Network/loadBalancers/inboundNatRules",
                "properties": {
                  "backendPort": 22,
                  "enableTcpReset": false,
                  "frontendIPConfiguration": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/frontendIPConfigurations/frontend-internal" },
                  "frontendPort": 50000,
                  "idleTimeoutInMinutes": 4,
                  "protocol": "Tcp"
                }
              }
            ],
            "loadBalancingRules": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/loadBalancingRules/tcp-80",
                "name": "tcp-80",
                "type": "Microsoft.Network/loadBalancers/loadBalancingRules",
                "properties": {
                  "backendAddressPool": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/backendAddressPools/kubernetes" },
                  "backendPort": 80,
                  "disableOutboundSnat": true,
                  "enableFloatingIP": true,
                  "enableTcpReset": true,
                  "frontendIPConfiguration": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/frontendIPConfigurations/frontend-internal" },
                  "frontendPort": 80,
                  "idleTimeoutInMinutes": 4,
                  "loadDistribution": "Default",
                  "probe": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/probes/tcp-80" },
                  "protocol": "Tcp",
                  "provisioningState": "Succeeded"
                }
              }
            ],
            "outboundRules": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/outboundRules/egress",
                "name": "egress",
                "type": "Microsoft.Network/loadBalancers/outboundRules",
                "properties": {
                  "allocatedOutboundPorts": 1024,
                  "backendAddressPool": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/backendAddressPools/kubernetes" },
                  "enableTcpReset": true,
                  "frontendIPConfigurations": [
                    { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/frontendIPConfigurations/frontend-internal" }
                  ],
                  "idleTimeoutInMinutes": 4,
                  "protocol": "All"
                }
              }
            ],
            "probes": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb-one/probes/tcp-80",
                "name": "tcp-80",
                "type": "Microsoft.Network/loadBalancers/probes",
                "properties": {
                  "intervalInSeconds": 5,
                  "numberOfProbes": 2,
                  "port": 31103,
                  "probeThreshold": 2,
                  "protocol": "Tcp",
                  "provisioningState": "Succeeded"
                }
              }
            ]
          }
        }
        """;

    private const string AccountJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-one",
          "name": "anf-one",
          "type": "microsoft.netapp/netappaccounts",
          "location": "westeurope",
          "properties": {
            "encryption": { "keySource": "Microsoft.NetApp" },
            "multiADStatus": "Enabled",
            "provisioningState": "Succeeded"
          }
        }
        """;

    private const string PoolJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-one/capacityPools/pool-ultra",
          "name": "anf-one/pool-ultra",
          "type": "microsoft.netapp/netappaccounts/capacitypools",
          "location": "westeurope",
          "properties": {
            "coolAccess": false,
            "encryptionType": "Single",
            "poolId": "8f9780cd-a12c-3983-fae9-d68e872a59bf",
            "provisioningState": "Succeeded",
            "qosType": "Auto",
            "serviceLevel": "Ultra",
            "size": 5497558138880,
            "totalThroughputMibps": 640.0,
            "utilizedThroughputMibps": 301.5
          }
        }
        """;

    private const string EncryptedAccountJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-two",
          "name": "anf-two",
          "type": "microsoft.netapp/netappaccounts",
          "location": "westeurope",
          "properties": {
            "activeDirectories": [
              {
                "activeDirectoryId": "4a0f6b1c-1d2e-3f40-5a6b-7c8d9e0f1a2b",
                "aesEncryption": true,
                "allowLocalNfsUsersWithLdap": false,
                "dns": "10.200.0.4,10.200.0.5",
                "domain": "corp.example.org",
                "ldapOverTLS": false,
                "ldapSigning": true,
                "organizationalUnit": "OU=Storage,DC=corp,DC=example,DC=org",
                "smbServerName": "ANF-SMB",
                "status": "InUse",
                "username": "anf-join"
              }
            ],
            "disableShowmount": true,
            "encryption": {
              "identity": {
                "principalId": "8c9a1f2e-0b3d-4c5a-9e7f-1a2b3c4d5e6f",
                "userAssignedIdentity": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-netapp"
              },
              "keySource": "Microsoft.KeyVault",
              "keyVaultProperties": {
                "keyName": "cmk-netapp",
                "keyVaultResourceId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one",
                "keyVaultUri": "https://kv-one.vault.azure.net",
                "status": "Created"
              }
            },
            "nfsV4IDDomain": "corp.example.org",
            "provisioningState": "Succeeded"
          }
        }
        """;

    private const string VaultJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one",
          "name": "kv-one",
          "type": "Microsoft.KeyVault/vaults",
          "location": "westeurope",
          "properties": { "sku": { "name": "premium" }, "enablePurgeProtection": true }
        }
        """;

    private const string IdentityJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-netapp",
          "name": "id-netapp",
          "type": "Microsoft.ManagedIdentity/userAssignedIdentities",
          "location": "westeurope",
          "properties": {
            "clientId": "b41d7c88-2f3a-4d19-8c60-7e5a9b0d1c23",
            "principalId": "8c9a1f2e-0b3d-4c5a-9e7f-1a2b3c4d5e6f",
            "tenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47"
          }
        }
        """;

    private const string VolumeJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.NetApp/netAppAccounts/anf-one/capacityPools/pool-ultra/volumes/pvc-one",
          "name": "anf-one/pool-ultra/pvc-one",
          "type": "microsoft.netapp/netappaccounts/capacitypools/volumes",
          "location": "westeurope",
          "properties": {
            "avsDataStore": "Disabled",
            "coolAccess": false,
            "creationToken": "pvc-one",
            "encryptionKeySource": "Microsoft.NetApp",
            "exportPolicy": {
              "rules": [
                {
                  "allowedClients": "0.0.0.0/0",
                  "chownMode": "Restricted",
                  "cifs": false,
                  "hasRootAccess": true,
                  "kerberos5ReadOnly": false,
                  "nfsv3": false,
                  "nfsv41": true,
                  "ruleIndex": 1,
                  "unixReadOnly": false,
                  "unixReadWrite": true
                }
              ]
            },
            "fileSystemId": "af0d98da-f1a2-00f1-0b5f-f60c4af1d091",
            "kerberosEnabled": false,
            "ldapEnabled": false,
            "maximumNumberOfFiles": 1556473,
            "mountTargets": [
              { "fileSystemId": "af0d98da-f1a2-00f1-0b5f-f60c4af1d091", "ipAddress": "10.200.5.148", "mountTargetId": "af0d98da-f1a2-00f1-0b5f-f60c4af1d091" }
            ],
            "networkFeatures": "Basic",
            "protocolTypes": [ "NFSv4.1" ],
            "provisioningState": "Succeeded",
            "securityStyle": "Unix",
            "serviceLevel": "Ultra",
            "snapshotDirectoryVisible": false,
            "storageToNetworkProximity": "T1",
            "subnetId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-data",
            "throughputMibps": 6.25,
            "unixPermissions": "0777",
            "usageThreshold": 53687091200,
            "volumeType": ""
          }
        }
        """;
}

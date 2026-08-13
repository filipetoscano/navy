using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class SecurityGroupAndIdentityTest
{
    private const string GroupId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one";
    private const string IdentityId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-app";
    private const string NicId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app";
    private const string VaultId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one";

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
    public void NetworkSecurityGroup_IsFullyMapped()
    {
        var group = Map<AzNetworkSecurityGroup>( GroupJson );

        Assert.Equal( "Succeeded", group.ProvisioningState );
        Assert.False( group.FlushConnection );
        Assert.Equal( 2, group.SecurityRules.Count );

        var allow = group.SecurityRules[ 0 ];

        Assert.Equal( "allow-https-in", allow.Name );
        Assert.Equal( "Microsoft.Network/networkSecurityGroups/securityRules", allow.Type );
        Assert.Equal( "Ingress from the gateway subnet", allow.Description );
        Assert.Equal( "Inbound", allow.Direction );
        Assert.Equal( "Allow", allow.Access );
        Assert.Equal( 100, allow.Priority );
        Assert.Equal( "Tcp", allow.Protocol );
        Assert.Equal( [ "10.0.1.0/24" ], allow.SourceAddressPrefixes );
        Assert.Equal( [ "*" ], allow.SourcePortRanges );
        Assert.Equal( [ "*" ], allow.DestinationAddressPrefixes );
        Assert.Equal( [ "443" ], allow.DestinationPortRanges );
    }


    /// <summary />
    /// <remarks>
    /// A rule written against several addresses or ports reports them as a list
    /// and leaves the single-valued form empty; both forms have to land in the
    /// same place on the model.
    /// </remarks>
    [Fact]
    public void NetworkSecurityGroup_MultiValuedRule_IsCollapsedOntoLists()
    {
        var group = Map<AzNetworkSecurityGroup>( GroupJson );

        var deny = group.SecurityRules[ 1 ];

        Assert.Equal( "Deny", deny.Access );
        Assert.Equal( 4000, deny.Priority );
        Assert.Equal( [ "10.0.2.0/24", "10.0.3.0/24" ], deny.SourceAddressPrefixes );
        Assert.Equal( [ "1433", "3306" ], deny.DestinationPortRanges );
        Assert.Equal( [ "Sql" ], deny.DestinationAddressPrefixes );
        Assert.Equal( [ "*" ], deny.SourcePortRanges );
        Assert.Null( deny.Description );
    }


    /// <summary />
    /// <remarks>
    /// A rule may name application security groups in place of an address.
    /// Those are not modelled, so only their identifiers are kept.
    /// </remarks>
    [Fact]
    public void NetworkSecurityGroup_ApplicationSecurityGroups_AreKeptAsIds()
    {
        var group = Map<AzNetworkSecurityGroup>( GroupJson );

        var deny = group.SecurityRules[ 1 ];

        Assert.Equal( [ "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/applicationSecurityGroups/asg-db" ], deny.DestinationApplicationSecurityGroupIds );
        Assert.Empty( deny.SourceApplicationSecurityGroupIds );
    }


    /// <summary />
    /// <remarks>
    /// The default rules are the same in every security group in every
    /// subscription, and say nothing about the inventory.
    /// </remarks>
    [Fact]
    public void NetworkSecurityGroup_DefaultRules_AreNotMapped()
    {
        var group = Map<AzNetworkSecurityGroup>( GroupJson );

        Assert.DoesNotContain( group.SecurityRules, x => x.Name == "AllowVnetInBound" );
    }


    /// <summary />
    /// <remarks>
    /// A subnet and an interface each already carry the group which applies to
    /// them, so the group must not resolve them back or the graph would close a
    /// loop and serialization would not terminate.
    /// </remarks>
    [Fact]
    public void NetworkSecurityGroup_AttachmentsAreNotResolved()
    {
        var group = Map<AzNetworkSecurityGroup>( GroupJson );
        var network = Network();
        var nic = Map<AzNetworkInterface>( NicJson );

        network.Subnets[ 0 ].NetworkSecurityGroupId = GroupId;

        Linker.Link( [ group, network, nic ] );

        Assert.Same( group, network.Subnets[ 0 ].NetworkSecurityGroup );
        Assert.Same( group, nic.NetworkSecurityGroup );
        Assert.Equal( [ SubnetId ], group.SubnetIds );
        Assert.Equal( [ NicId ], group.NetworkInterfaceIds );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ group, network, nic ] );

        Assert.Contains( "nsg-one", json );
    }


    /// <summary />
    [Fact]
    public void ManagedIdentity_IsFullyMapped()
    {
        var identity = Map<AzManagedIdentity>( IdentityJson );

        Assert.Equal( "id-app", identity.Name );
        Assert.Equal( "prod", identity.Tags[ "env" ] );
        Assert.Equal( "8c9a1f2e-0b3d-4c5a-9e7f-1a2b3c4d5e6f", identity.PrincipalId );
        Assert.Equal( "b41d7c88-2f3a-4d19-8c60-7e5a9b0d1c23", identity.ClientId );
        Assert.Equal( "72f988bf-86f1-41af-91ab-2d7cd011db47", identity.TenantId );
    }


    /// <summary />
    /// <remarks>
    /// The identity a SQL server uses to reach its customer-managed key is the
    /// one relationship which points at a user-assigned identity.
    /// </remarks>
    [Fact]
    public void ManagedIdentity_IsResolvedByTheServerWhichUsesIt()
    {
        var identity = Map<AzManagedIdentity>( IdentityJson );
        var server = Map<AzSqlServer>( ServerJson );

        Linker.Link( [ identity, server ] );

        Assert.Equal( IdentityId, server.PrimaryUserAssignedIdentityId );
        Assert.Same( identity, server.PrimaryUserAssignedIdentity );
    }


    /// <summary />
    /// <remarks>
    /// The identity may have been created in another subscription and handed to
    /// the server, in which case there is nothing to resolve it to.
    /// </remarks>
    [Fact]
    public void ManagedIdentity_FromAnotherSubscription_IsLeftNull()
    {
        var server = Map<AzSqlServer>( ServerJson );

        Linker.Link( [ server ] );

        Assert.Equal( IdentityId, server.PrimaryUserAssignedIdentityId );
        Assert.Null( server.PrimaryUserAssignedIdentity );
    }


    /// <summary />
    [Fact]
    public void DiskEncryptionSet_IsFullyMapped()
    {
        var set = Map<AzDiskEncryptionSet>( EncryptionSetJson );

        Assert.Equal( "des-one", set.Name );
        Assert.Equal( "EncryptionAtRestWithCustomerKey", set.EncryptionType );
        Assert.Equal( "Succeeded", set.ProvisioningState );
        Assert.True( set.RotationToLatestKeyVersionEnabled );
        Assert.Equal( "None", set.FederatedClientId );

        Assert.Equal( VaultId, set.KeyVaultId );
        Assert.Equal( "https://kv-one.vault.azure.net/keys/cmk-disks/6f2c1b9a4d3e4f80b1c2d3e4f5a6b7c8", set.KeyUrl );
        Assert.Equal( 2026, set.LastKeyRotationTimestamp!.Value.Year );
    }


    /// <summary />
    [Fact]
    public void DiskEncryptionSet_KeyVaultIsResolved()
    {
        var set = Map<AzDiskEncryptionSet>( EncryptionSetJson );
        var vault = Map<AzKeyVault>( VaultJson );

        Linker.Link( [ set, vault ] );

        Assert.Same( vault, set.KeyVault );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ set, vault ] );

        Assert.Contains( "kv-one", json );
    }


    /// <summary />
    /// <remarks>
    /// A key held in a managed HSM has no source vault, and the key URL is then
    /// the only record of where it lives.
    /// </remarks>
    [Fact]
    public void DiskEncryptionSet_WithoutSourceVault_KeepsTheKeyUrl()
    {
        var set = Map<AzDiskEncryptionSet>( ManagedHsmSetJson );

        Linker.Link( [ set ] );

        Assert.Null( set.KeyVaultId );
        Assert.Null( set.KeyVault );
        Assert.Equal( "https://hsm-one.managedhsm.azure.net/keys/cmk-disks/9a8b7c6d", set.KeyUrl );
        Assert.Null( set.LastKeyRotationTimestamp );
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Activator.CreateInstance<AzVirtualNetwork>();

        network.Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one";
        network.Name = "vnet-one";
        network.Type = "Microsoft.Network/virtualNetworks";
        network.Location = "westeurope";
        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-app",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.0.1.0/24",
            },
        ];

        return network;
    }


    private const string GroupJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one",
          "name": "nsg-one",
          "type": "Microsoft.Network/networkSecurityGroups",
          "location": "westeurope",
          "properties": {
            "provisioningState": "Succeeded",
            "resourceGuid": "1f0a2b3c-4d5e-6f70-8192-a3b4c5d6e7f8",
            "flushConnection": false,
            "securityRules": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one/securityRules/allow-https-in",
                "name": "allow-https-in",
                "type": "Microsoft.Network/networkSecurityGroups/securityRules",
                "properties": {
                  "provisioningState": "Succeeded",
                  "description": "Ingress from the gateway subnet",
                  "protocol": "Tcp",
                  "sourcePortRange": "*",
                  "destinationPortRange": "443",
                  "sourceAddressPrefix": "10.0.1.0/24",
                  "destinationAddressPrefix": "*",
                  "access": "Allow",
                  "priority": 100,
                  "direction": "Inbound",
                  "sourcePortRanges": [],
                  "destinationPortRanges": [],
                  "sourceAddressPrefixes": [],
                  "destinationAddressPrefixes": []
                }
              },
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one/securityRules/deny-database-out",
                "name": "deny-database-out",
                "type": "Microsoft.Network/networkSecurityGroups/securityRules",
                "properties": {
                  "provisioningState": "Succeeded",
                  "protocol": "Tcp",
                  "sourcePortRange": "*",
                  "destinationPortRange": "",
                  "sourceAddressPrefix": "",
                  "destinationAddressPrefix": "Sql",
                  "access": "Deny",
                  "priority": 4000,
                  "direction": "Outbound",
                  "sourcePortRanges": [],
                  "destinationPortRanges": [ "1433", "3306" ],
                  "sourceAddressPrefixes": [ "10.0.2.0/24", "10.0.3.0/24" ],
                  "destinationAddressPrefixes": [],
                  "destinationApplicationSecurityGroups": [
                    { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/applicationSecurityGroups/asg-db" }
                  ]
                }
              }
            ],
            "defaultSecurityRules": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one/defaultSecurityRules/AllowVnetInBound",
                "name": "AllowVnetInBound",
                "type": "Microsoft.Network/networkSecurityGroups/defaultSecurityRules",
                "properties": {
                  "description": "Allow inbound traffic from all VMs in VNET",
                  "protocol": "*",
                  "sourcePortRange": "*",
                  "destinationPortRange": "*",
                  "sourceAddressPrefix": "VirtualNetwork",
                  "destinationAddressPrefix": "VirtualNetwork",
                  "access": "Allow",
                  "priority": 65000,
                  "direction": "Inbound"
                }
              }
            ],
            "subnets": [ { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app" } ],
            "networkInterfaces": [ { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one" } ]
          }
        }
        """;

    private const string NicJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one",
          "name": "nic-one",
          "type": "Microsoft.Network/networkInterfaces",
          "location": "westeurope",
          "properties": {
            "provisioningState": "Succeeded",
            "macAddress": "00-0D-3A-1B-2C-3D",
            "nicType": "Standard",
            "networkSecurityGroup": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one" },
            "ipConfigurations": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one/ipConfigurations/ipconfig1",
                "name": "ipconfig1",
                "properties": {
                  "privateIPAddress": "10.0.1.4",
                  "privateIPAllocationMethod": "Dynamic",
                  "primary": true,
                  "subnet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app" }
                }
              }
            ]
          }
        }
        """;

    private const string IdentityJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-app",
          "name": "id-app",
          "type": "Microsoft.ManagedIdentity/userAssignedIdentities",
          "location": "westeurope",
          "tags": { "env": "prod" },
          "properties": {
            "tenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
            "principalId": "8c9a1f2e-0b3d-4c5a-9e7f-1a2b3c4d5e6f",
            "clientId": "b41d7c88-2f3a-4d19-8c60-7e5a9b0d1c23"
          }
        }
        """;

    private const string ServerJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Sql/servers/sql-one",
          "name": "sql-one",
          "type": "Microsoft.Sql/servers",
          "location": "westeurope",
          "kind": "v12.0",
          "properties": {
            "version": "12.0",
            "state": "Ready",
            "primaryUserAssignedIdentityId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-app"
          }
        }
        """;

    private const string EncryptionSetJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one",
          "name": "des-one",
          "type": "Microsoft.Compute/diskEncryptionSets",
          "location": "westeurope",
          "identity": { "type": "SystemAssigned", "principalId": "3a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9" },
          "properties": {
            "activeKey": {
              "sourceVault": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one" },
              "keyUrl": "https://kv-one.vault.azure.net/keys/cmk-disks/6f2c1b9a4d3e4f80b1c2d3e4f5a6b7c8"
            },
            "encryptionType": "EncryptionAtRestWithCustomerKey",
            "rotationToLatestKeyVersionEnabled": true,
            "lastKeyRotationTimestamp": "2026-03-11T08:14:22.7654321Z",
            "provisioningState": "Succeeded",
            "federatedClientId": "None"
          }
        }
        """;

    private const string ManagedHsmSetJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-hsm",
          "name": "des-hsm",
          "type": "Microsoft.Compute/diskEncryptionSets",
          "location": "westeurope",
          "properties": {
            "activeKey": { "keyUrl": "https://hsm-one.managedhsm.azure.net/keys/cmk-disks/9a8b7c6d" },
            "encryptionType": "EncryptionAtRestWithPlatformAndCustomerKeys",
            "rotationToLatestKeyVersionEnabled": false,
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
          "properties": {
            "sku": { "family": "A", "name": "premium" },
            "enabledForDiskEncryption": true,
            "enablePurgeProtection": true,
            "enableSoftDelete": true,
            "softDeleteRetentionInDays": 90
          }
        }
        """;
}

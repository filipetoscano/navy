namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The autoscaler profile, which is some twenty tuning knobs, is deliberately
/// not mapped: it describes how the cluster behaves rather than what it is.
/// </remarks>
public class AzKubernetesService : AzResource
{
    /// <summary />
    /// <remarks>
    /// Base or Automatic.
    /// </remarks>
    public string? Sku { get; set; }

    /// <summary />
    /// <remarks>
    /// Free, Standard or Premium. What the control plane is charged at, and
    /// whether it carries an availability guarantee.
    /// </remarks>
    public string? SkuTier { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    /// <remarks>
    /// Running or Stopped. A stopped cluster keeps its configuration and its
    /// disks, and is not charged for its nodes.
    /// </remarks>
    public string? PowerState { get; set; }


    /// <summary>
    /// Version asked for, which may name only a major and a minor.
    /// </summary>
    public string? KubernetesVersion { get; set; }

    /// <summary>
    /// Version the control plane is actually running.
    /// </summary>
    public string? CurrentKubernetesVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// rapid, stable, patch, node-image or none.
    /// </remarks>
    public string? UpgradeChannel { get; set; }

    /// <summary />
    public string? NodeOSUpgradeChannel { get; set; }

    /// <summary />
    /// <remarks>
    /// KubernetesOfficial, or AKSLongTermSupport for a cluster kept on a
    /// version Kubernetes itself no longer supports.
    /// </remarks>
    public string? SupportPlan { get; set; }


    /// <summary />
    public string? DnsPrefix { get; set; }

    /// <summary>
    /// Address of the API server.
    /// </summary>
    /// <remarks>
    /// Null on a private cluster, which reports
    /// <see cref="PrivateFqdn" /> instead.
    /// </remarks>
    public string? Fqdn { get; set; }

    /// <summary />
    public string? PrivateFqdn { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the API server is reachable only from the virtual network.
    /// </remarks>
    public bool EnablePrivateCluster { get; set; }

    /// <summary>
    /// Private DNS zone which resolves the API server.
    /// </summary>
    /// <remarks>
    /// System for a zone Azure manages, otherwise the identifier of a zone.
    /// Zones are not modelled, so this does not resolve.
    /// </remarks>
    public string? PrivateDnsZone { get; set; }

    /// <summary>
    /// Addresses allowed to reach a public API server.
    /// </summary>
    /// <remarks>
    /// Empty means every address, which on a public cluster is worth noticing.
    /// </remarks>
    public List<string> AuthorizedIPRanges { get; set; } = [];


    /// <summary>
    /// Resource group Azure creates to hold the nodes, disks and balancers.
    /// </summary>
    /// <remarks>
    /// Its contents belong to the cluster rather than to whoever created it,
    /// and appear in the inventory as ordinary resources of that group.
    /// </remarks>
    public string? NodeResourceGroup { get; set; }

    /// <summary />
    public bool EnableRbac { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the local Kubernetes administrator account is refused and
    /// every user has to come through Entra.
    /// </remarks>
    public bool DisableLocalAccounts { get; set; }

    /// <summary />
    /// <remarks>
    /// True when Kubernetes roles are granted through Azure RBAC rather than
    /// through role bindings inside the cluster.
    /// </remarks>
    public bool EnableAzureRbac { get; set; }

    /// <summary />
    public List<string> AadAdminGroupObjectIds { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// True when a pod may hold an Entra identity of its own.
    /// </remarks>
    public bool WorkloadIdentityEnabled { get; set; }

    /// <summary />
    public bool OidcIssuerEnabled { get; set; }

    /// <summary />
    public bool DefenderEnabled { get; set; }


    /// <summary />
    /// <remarks>
    /// azure, kubenet or none.
    /// </remarks>
    public string? NetworkPlugin { get; set; }

    /// <summary />
    /// <remarks>
    /// overlay, on a cluster whose pods have addresses of their own rather than
    /// addresses from the subnet.
    /// </remarks>
    public string? NetworkPluginMode { get; set; }

    /// <summary />
    /// <remarks>
    /// azure, calico, cilium or none. None means any pod may reach any pod.
    /// </remarks>
    public string? NetworkPolicy { get; set; }

    /// <summary />
    public string? NetworkDataplane { get; set; }

    /// <summary />
    /// <remarks>
    /// loadBalancer, userDefinedRouting, managedNATGateway or
    /// userAssignedNATGateway. Which of them applies decides how the nodes
    /// reach the internet, and whether a firewall sees that traffic.
    /// </remarks>
    public string? OutboundType { get; set; }

    /// <summary />
    public string? LoadBalancerSku { get; set; }

    /// <summary />
    public List<string> PodCidrs { get; set; } = [];

    /// <summary />
    public List<string> ServiceCidrs { get; set; } = [];

    /// <summary />
    public string? DnsServiceIP { get; set; }

    /// <summary />
    /// <remarks>
    /// Istio on a cluster running the managed service mesh, null otherwise.
    /// </remarks>
    public string? ServiceMeshMode { get; set; }


    /// <summary>
    /// Set which encrypts the node disks with a customer-managed key.
    /// </summary>
    public string? DiskEncryptionSetId { get; set; }

    /// <summary>
    /// Identity the nodes use to pull images and talk to Azure.
    /// </summary>
    /// <remarks>
    /// Created by Azure in the node resource group, and so commonly outside the
    /// subscription being read.
    /// </remarks>
    public string? KubeletIdentityId { get; set; }


    /// <summary />
    public List<AzKubernetesNodePool> NodePools { get; set; } = [];

    /// <summary>
    /// Add-ons Azure installs into the cluster.
    /// </summary>
    /// <remarks>
    /// Only those which are turned on are kept: Azure reports every add-on it
    /// knows about, most of them disabled.
    /// </remarks>
    public List<AzKubernetesAddon> Addons { get; set; } = [];


    /// <summary />
    public AzDiskEncryptionSet? DiskEncryptionSet { get; set; }

    /// <summary />
    public AzManagedIdentity? KubeletIdentity { get; set; }
}


/// <summary />
/// <remarks>
/// Azure calls these agent pools, and each one is backed by a scale set which
/// appears in the inventory in its own right, in the node resource group.
/// </remarks>
public class AzKubernetesNodePool : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// System for a pool which may run the cluster's own components, User for
    /// one which may not.
    /// </remarks>
    public string? Mode { get; set; }

    /// <summary />
    public int Count { get; set; }

    /// <summary />
    public string? VmSize { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    public string? PowerState { get; set; }


    /// <summary />
    public bool EnableAutoScaling { get; set; }

    /// <summary />
    public int MinCount { get; set; }

    /// <summary />
    public int MaxCount { get; set; }

    /// <summary>
    /// Most pods which may be scheduled onto one node.
    /// </summary>
    public int MaxPods { get; set; }

    /// <summary />
    public List<string> AvailabilityZones { get; set; } = [];


    /// <summary />
    /// <remarks>
    /// Linux or Windows.
    /// </remarks>
    public string? OsType { get; set; }

    /// <summary />
    /// <remarks>
    /// AzureLinux, Ubuntu, Windows2022 and so on.
    /// </remarks>
    public string? OsSku { get; set; }

    /// <summary />
    public int OsDiskSizeGB { get; set; }

    /// <summary />
    /// <remarks>
    /// Managed or Ephemeral. An ephemeral disk lives on the node itself and is
    /// lost when the node is.
    /// </remarks>
    public string? OsDiskType { get; set; }

    /// <summary />
    public bool EnableEncryptionAtHost { get; set; }

    /// <summary />
    public bool EnableFips { get; set; }

    /// <summary />
    /// <remarks>
    /// True when each node is given a public address of its own.
    /// </remarks>
    public bool EnableNodePublicIP { get; set; }


    /// <summary />
    public string? OrchestratorVersion { get; set; }

    /// <summary />
    public string? CurrentOrchestratorVersion { get; set; }

    /// <summary />
    public string? NodeImageVersion { get; set; }


    /// <summary />
    public Dictionary<string, string> NodeLabels { get; set; } = [];

    /// <summary>
    /// Taints which keep pods off these nodes unless they tolerate them.
    /// </summary>
    public List<string> NodeTaints { get; set; } = [];


    /// <summary />
    public string? SubnetId { get; set; }

    /// <summary />
    public AzSubnet? Subnet { get; set; }
}


/// <summary />
public class AzKubernetesAddon
{
    /// <summary />
    /// <remarks>
    /// azureKeyvaultSecretsProvider, azurepolicy, omsagent and so on.
    /// </remarks>
    public required string Name { get; set; }

    /// <summary>
    /// Identity the add-on runs as.
    /// </summary>
    /// <remarks>
    /// Created by Azure in the node resource group, and so commonly outside the
    /// subscription being read.
    /// </remarks>
    public string? IdentityResourceId { get; set; }
}

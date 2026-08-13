navy
==========================================================================

Command line tool to generate, and report on/from, an inventory for an
Azure subscription.


Usage
--------------------------------------------------------------------------

```
> navy --help
0.1.0+ed612f538d962f4a871e36a3339451435ae2caeb

Azure Inventory

Usage: navy [command] [options]

Options:
  --version     Show version information.
  -v|--verbose  Enabled verbose output
  -?|-h|--help  Show help information.

Commands:
  build         Builds an inventory for one subscription
  network       Emits IP/resource network layout for an enviroment
  split         Splits an inventory into an environment

Run 'navy [command] -?|-h|--help' for more information about a command
```


Examples
--------------------------------------------------------------------------

```
navy build subscription-name --no-stitch --output-file=sub.json
navy split sub.json prd --output-file=prd.json
navy network sub.json prd
```

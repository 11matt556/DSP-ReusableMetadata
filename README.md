# Reusable Metadata Mod
## I reccomend backing up your save data and property file in Documents\Dyson Sphere Program\ before running this mod.

The mod changes Metadata to behave more like I envisioned when I first heard about a "NewGame+" mode in DSP.

By default, it changes the consumption calculations such other cluster addresses no longer reduces the amount of metadata available to your current cluster address. This allows you to start every new game with your maximum amount of metadata

Optionally, you can enable 'useHighestProductionOnly' to change the metadata production calculations as well. Enabling this option means that Metadata is no longer added together from all your clusters, but instead only metadata from your 'best' cluster is counted. With this enabled Metadata can be thought of as a 'high score'.

Partially inspired by reddit post https://www.reddit.com/r/Dyson_Sphere_Program/comments/u9l3wv/metadata_usage/

## Configuration Options


|Key|Type|Default|Description|
|---|---|---|---|
useHighestProductionOnly|bool|false|'High score' mode. Only the metadata contributions from your highest production cluster will be available. Metadata from multiple clusters is NOT added together."
verboseLogging|bool|false|For debugging purposes
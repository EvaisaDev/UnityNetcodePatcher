# Unity Netcode Patcher
**This is a BepInEx patcher which replicates the IL Post Processing that unity does with it's Netcode For Gameobjects Package, allowing you to create custom NetworkBehaviours in mods as if you were doing it in a Unity project.**

- This is somewhat experimental as it has not been tested properly yet.
- This was originally written for Lethal Company modding, and has only been tested with `com.unity.netcode.gameobjects@1.5.2`
- Currently only patches NetworkBehaviours. INetworkMessage and INetworkSerializable processing does not work yet.

*Note, this is intended to be a tool for modders, mods should be shipped after patching and this tool should not be installed by users.*

## Usage
1. Download the latest release from [Releases](https://github.com/EvaisaDev/UnityNetcodeWeaver/releases)
2. Move NetcodePatcher folder from the zip into `BepInEx/patchers`
3. When launching the game with BepInEx enabled it will automatically patch any installed plugins.
	- *Note that patching a plugin requires a PDB file with a matching name to the plugin to be in the same folder as the assembly.*

using System.Collections.Generic;

namespace RustAnalyzer.Configuration
{
    public static class HookConfiguration
    {
        private static readonly HashSet<string> KnownHooks = new()
        {
            // Server Hooks
            "OnServerInitialized",
            "OnServerSave",
            "OnServerShutdown",
            // Player Hooks
            "OnPlayerConnected",
            "OnPlayerDisconnected",
            "OnPlayerInit",
            "OnPlayerRespawned",
            "OnPlayerRespawn",
            "OnPlayerAttack",
            "OnPlayerHurt",
            "OnPlayerDeath",
            "OnPlayerLoot",
            "OnPlayerChat",
            // Entity Hooks
            "OnEntitySpawned",
            "OnEntityDeath",
            "OnEntityTakeDamage",
            "OnEntityBuilt",
            // Item Hooks
            "OnItemCraft",
            "OnItemCraftFinished",
            "OnItemPickup",
            "OnItemDrop",
            // Structure Hooks
            "OnStructureDemolish",
            "OnStructureUpgrade",
            "OnStructureRotate",
            // Resource Hooks
            "OnGatherItem",
            "OnDispenserGather",
            "OnQuarryGather",
            "OnCollectiblePickup",
            // Vehicle Hooks
            "OnVehicleSpawn",
            "OnVehicleDespawn",
            "OnVehicleEnter",
            "OnVehicleExit",
        };

        public static bool IsHookMethod(string methodName)
        {
            return KnownHooks.Contains(methodName);
        }
    }
}

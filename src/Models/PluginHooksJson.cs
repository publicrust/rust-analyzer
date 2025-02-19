using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Models
{
    public static class PluginHooksJson
    {
        private static readonly List<Tuple<string, string>> Hooks = new List<Tuple<string, string>>
        {
            Tuple.Create("Vanish", "OnVanishReappear(BasePlayer)"),
            Tuple.Create("IQChat", "OnPlayerMuted(BasePlayer,BasePlayer,int,String)"),
            Tuple.Create("aTimeAPI", "OnRealSecond()"),
            Tuple.Create("aTimeAPI", "OnRustDayStarted()"),
            Tuple.Create("aTimeAPI", "OnRustNightStarted()"),
            Tuple.Create("aTimeAPI", "OnNewRealHourStarted(int)"),
            Tuple.Create("aTimeAPI", "OnNewRealDayStarted(int)"),
            Tuple.Create("aTimeAPI", "OnNewRealMonthStarted(int)"),
            Tuple.Create("aTimeAPI", "OnNewRealYearStarted(int)"),
            Tuple.Create("IQChat", "OnModeratorSendBadWords(BasePlayer,string)"),
            Tuple.Create("IQChat", "OnPlayerSendBadWords(BasePlayer,string)"),
            Tuple.Create("IQTeleportation", "OnHomeRemoved(BasePlayer,Vector3,string)"),
            Tuple.Create("IQTeleportation", "OnHomeAdded(BasePlayer,Vector3,string)"),
            Tuple.Create("IQTeleportation", "OnHomeAccepted(BasePlayer,string,int)"),
            Tuple.Create("IQTeleportation", "OnHomeAccepted(BasePlayer,string,int)"),
            Tuple.Create("IQTeleportation", "OnPlayerTeleported(BasePlayer,Vector3,Vector3)"),
            Tuple.Create("IQTeleportation", "OnTeleportAccepted(BasePlayer,BasePlayer,int)"),
            Tuple.Create("IQTeleportation", "OnTeleportRejected(BasePlayer,BasePlayer)"),
            Tuple.Create("IQTeleportation", "CanTeleport(BasePlayer)"),
            Tuple.Create("IQTeleportation", "canTeleport(BasePlayer)"),
            Tuple.Create("IQTeleportation", "OnTeleportRejected(BasePlayer,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnPlayerTeleported(BasePlayer,Vector3,Vector3)"),
            Tuple.Create("NTeleportation", "OnTeleportRejected(BasePlayer,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnTeleportRequestCompleted(BasePlayer,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnTeleportAccepted(BasePlayer,BasePlayer,int)"),
            Tuple.Create("NTeleportation", "OnHomeAccepted(BasePlayer,string,int)"),
            Tuple.Create("NTeleportation", "OnHomeRemoved(BasePlayer,Vector3,string)"),
            Tuple.Create("NTeleportation", "OnHomeAdded(BasePlayer,Vector3,string)"),
            Tuple.Create("NTeleportation", "OnTeleportInterrupted(BasePlayer,string,ulong,string)"),
            Tuple.Create("IQPermissions", "SetPermission(ulong,string,DateTime)"),
            Tuple.Create("IQPermissions", "SetPermission(ulong,string,string)"),
            Tuple.Create("IQPermissions", "SetGroup(ulong,string,DateTime)"),
            Tuple.Create("IQPermissions", "SetGroup(ulong,string,string)"),
            Tuple.Create("IQPermissions", "RevokePermission(ulong,string)"),
            Tuple.Create("IQPermissions", "RevokeGroup(ulong,string,DateTime)"),
            Tuple.Create("IQBanSystem", "OnKickPlayer(string,string,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnBannedPlayerIP(string,string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnBannedPlayerID(ulong,string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnUpdateTimeBannedID(string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnUpdateTimeBannedIP(string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnChangePermanentBannedID(string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnChangePermanentBannedIP(string,double,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnUnbannedID(string,BasePlayer)"),
            Tuple.Create("IQBanSystem", "OnUnbannedIP(string,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnTeleportInterrupted(BasePlayer,string,ulong,string)"),
            Tuple.Create("NTeleportation", "OnHomeAdded(BasePlayer,Vector3,string)"),
            Tuple.Create("NTeleportation", "OnHomeRemoved(BasePlayer,Vector3,string)"),
            Tuple.Create("NTeleportation", "OnHomeAccepted(BasePlayer,string,int)"),
            Tuple.Create("NTeleportation", "OnTeleportAccepted(BasePlayer,BasePlayer,int)"),
            Tuple.Create("NTeleportation", "OnTeleportRequestCompleted(BasePlayer,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnTeleportRejected(BasePlayer,BasePlayer)"),
            Tuple.Create("NTeleportation", "OnPlayerTeleported(BasePlayer,Vector3,Vector3)"),
            Tuple.Create("NoEscape", "StartRaidBlocking(BasePlayer,Vector3,bool)"),
            Tuple.Create("NoEscape", "StopBlocking(BasePlayer)"),
            Tuple.Create("NoEscape", "StopRaidBlocking(string)"),
            Tuple.Create("NoEscape", "StartCombatBlocking(BasePlayer)"),
            Tuple.Create("NoEscape", "StopCombatBlocking(string)"),
            Tuple.Create("XDQuest", "OnQuestCompleted(BasePlayer,string)"),
            Tuple.Create(
                "XDQuest",
                "OnQuestProgress(ulong,QuestType,string,string,List<Item>,int)"
            ),
            Tuple.Create("TimedPermissions", "OnTimedPermissionGranted(string,string,TimeSpan)"),
            Tuple.Create("TimedPermissions", "OnTimedPermissionExtended(string,string,TimeSpan)"),
            Tuple.Create("TimedPermissions", "OnTimedGroupAdded(string,string,TimeSpan)"),
            Tuple.Create("TimedPermissions", "OnTimedGroupExtended(string,string,TimeSpan)"),
            Tuple.Create("ZoneManager", "SetZoneStatus(string,bool)"),
            Tuple.Create("ZoneManager", "AddFlag(string,string)"),
            Tuple.Create("ZoneManager", "RemoveFlag(string,string)"),
            Tuple.Create("ZoneManager", "AddDisabledFlag(string,string)"),
            Tuple.Create("ZoneManager", "RemoveDisabledFlag(string,string)"),
            Tuple.Create("ZoneManager", "OnEnterZone(string,BasePlayer)"),
            Tuple.Create("ZoneManager", "OnExitZone(string,BasePlayer)"),
            Tuple.Create("ZoneManager", "OnEntityEnterZone(string,BaseEntity)"),
            Tuple.Create("ZoneManager", "OnEntityExitZone(string,BaseEntity)"),
            Tuple.Create("Kits", "GetKitNames(List<string>)"),
            Tuple.Create("Kits", "SetPlayerCooldown(ulong,string,double)"),
            Tuple.Create("Kits", "SetPlayerKitUses(ulong,string,int)"),
            Tuple.Create("Backpacks", "OnBackpackOpened(BasePlayer,ulong,ItemContainer)"),
            Tuple.Create("Backpacks", "OnBackpackClosed(BasePlayer,ulong,ItemContainer)"),
            Tuple.Create("RaidableBases", "OnRaidableBaseStarted(Vector3,int,float)"),
            Tuple.Create("RaidableBases", "OnRaidableBaseEnded(Vector3,int,float)"),
            Tuple.Create("RaidableBases", "OnPlayerEnteredRaidableBase(BasePlayer,Vector3,bool)"),
            Tuple.Create("RaidableBases", "OnPlayerExitedRaidableBase(BasePlayer,Vector3,bool)"),
            Tuple.Create("FClan", "GetPointRaid(ulong,ulong)"),
            Tuple.Create("Friends", "IsFriendOf(ulong)"),
            Tuple.Create("Friends", "GetFriendList(ulong)"),
            Tuple.Create("Friends", "GetFriendList(string)"),
            Tuple.Create("Friends", "GetFriends(ulong)"),
            Tuple.Create("Friends", "GetMaxFriends()"),
            Tuple.Create("Friends", "IsFriend(ulong,ulong)"),
            Tuple.Create("Friends", "RemoveFriend(ulong,ulong)"),
            Tuple.Create("Friends", "AddFriend(ulong,ulong)"),
            Tuple.Create("Friends", "AreFriends(ulong,ulong)"),
            Tuple.Create("Friends", "HasFriend(ulong,ulong)"),
            Tuple.Create("IQCases", "OnOpenedCase(BasePlayer,string)"),
            Tuple.Create("IQCases", "OnBuyCase(BasePlayer,string)"),
            Tuple.Create("IQCases", "OnSellCase(BasePlayer,string)"),
        };

        public static List<PluginHookModel> GetHooks()
        {
            return new List<PluginHookModel>();
        //     try
        //     {
        //     //     return Hooks
        //     //         .Select(h =>
        //     //         {
        //     //             var hookModel = HooksUtils.ParseHookString(h.Item2);
        //     //             if (hookModel == null)
        //     //                 return null;

        //     //             return new PluginHookModel
        //     //             {
        //     //                 PluginName = h.Item1,
        //     //                 Name = hookModel.Name,
        //     //                 Parameters = hookModel.Parameters,
        //     //             };
        //     //         })
        //     //         .Where(h => h != null)
        //     //         .Select(h => h!)
        //     //         .ToList();
        //     // }
        //     // catch (Exception ex)
        //     // {
        //     //     Console.WriteLine(
        //     //         $"[RustAnalyzer] Failed to load plugin hooks PluginHooksJson {ex.Message}"
        //     //     );
        //     //     return new List<PluginHookModel>();
        //     // }
        }
    }
}

using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.Hooks.Interfaces;
using System;
using System.Reflection;

namespace RustAnalyzer.src.Hooks.Providers
{
    [Version("Universal")]
    public class HooksUniversalProvider : BaseJsonHooksProvider 
    {
        protected override string JsonContent => @"{
  ""hooks"": [
    ""CanUserLogin(string name, string id, string ipAddress)"",
    ""OnServerInitialized(bool initial)"",
    ""OnPermissionRegistered(string name, Plugin owner)"",
    ""OnGroupPermissionRevoked(string name, string perm)"",
    ""OnGroupCreated(string name)"",
    ""OnUserRespawned(IPlayer player)"",
    ""OnFrame()"",
    ""OnGroupTitleSet(string name, string title)"",
    ""OnUserPermissionGranted(string id, string permName)"",
    ""OnUserCommand(IPlayer player, string command, string[] args)"",
    ""OnPluginUnloaded(Plugin plugin)"",
    ""OnGroupParentSet(string name, string parent)"",
    ""OnServerSave()"",
    ""OnUserPermissionRevoked(string id, string permName)"",
    ""OnUserApproved(string name, string id, string ipAddress)"",
    ""OnUserConnected(IPlayer player)"",
    ""LoadDefaultMessages()"",
    ""OnPluginLoaded(Plugin plugin)"",
    ""OnUserDisconnected(IPlayer player)"",
    ""OnGroupDeleted(string name)"",
    ""Unload()"",
    ""OnUserRespawn(IPlayer player)"",
    ""LoadDefaultConfig()"",
    ""OnGroupPermissionGranted(string name, string perm)"",
    ""OnUserNameUpdated(string id, string oldName, string newName)"",
    ""OnUserGroupRemoved(string id, string groupName)"",
    ""Init()"",
    ""OnUserUnbanned(string name, string id, string ipAddress)"",
    ""OnUserBanned(string name, string id, string ipAddress, string reason)"",
    ""OnGroupRankSet(string name, int rank)"",
    ""Loaded()"",
    ""OnUserKicked(IPlayer player, string reason)"",
    ""OnUserGroupAdded(string id, string groupName)"",
    ""OnUserChat(IPlayer player, string message)""
  ]
}";
    }
}    

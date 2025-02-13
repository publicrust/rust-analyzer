using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using RustAnalyzer.Models;
using RustAnalyzer.Utils;

namespace RustAnalyzer
{
    internal static class UnityHooksJson
    {
        /// <summary>
        /// Provides access to the Rust plugin hook definitions.
        /// </summary>
        public const string Json =
            "{ \"hooks\": [\r\n  \"Awake()\",\r\n  \"OnEnable()\",\r\n  \"Start()\",\r\n  \"OnDisable()\",\r\n  \"OnDestroy()\",\r\n  \"Update()\",\r\n  \"LateUpdate()\",\r\n  \"FixedUpdate()\",\r\n  \"OnApplicationFocus(bool)\",\r\n  \"OnApplicationPause(bool)\",\r\n  \"OnApplicationQuit()\",\r\n  \"OnBecameVisible()\",\r\n  \"OnBecameInvisible()\",\r\n  \"OnPreCull()\",\r\n  \"OnPreRender()\",\r\n  \"OnRenderObject()\",\r\n  \"OnPostRender()\",\r\n  \"OnRenderImage(RenderTexture,RenderTexture)\",\r\n  \"OnDrawGizmos()\",\r\n  \"OnDrawGizmosSelected()\",\r\n  \"OnGUI()\",\r\n  \"OnMouseDown()\",\r\n  \"OnMouseUp()\",\r\n  \"OnMouseUpAsButton()\",\r\n  \"OnMouseEnter()\",\r\n  \"OnMouseOver()\",\r\n  \"OnMouseExit()\",\r\n  \"OnMouseDrag()\",\r\n  \"OnCollisionEnter(Collision)\",\r\n  \"OnCollisionStay(Collision)\",\r\n  \"OnCollisionExit(Collision)\",\r\n  \"OnTriggerEnter(Collider)\",\r\n  \"OnTriggerStay(Collider)\",\r\n  \"OnTriggerExit(Collider)\",\r\n  \"OnCollisionEnter2D(Collision2D)\",\r\n  \"OnCollisionStay2D(Collision2D)\",\r\n  \"OnCollisionExit2D(Collision2D)\",\r\n  \"OnTriggerEnter2D(Collider2D)\",\r\n  \"OnTriggerStay2D(Collider2D)\",\r\n  \"OnTriggerExit2D(Collider2D)\",\r\n  \"OnControllerColliderHit(ControllerColliderHit)\",\r\n  \"OnParticleCollision(GameObject)\",\r\n  \"OnParticleTrigger()\",\r\n  \"OnAnimatorMove()\",\r\n  \"OnAnimatorIK(int)\",\r\n  \"OnAudioFilterRead(float[],int)\",\r\n  \"OnTransformChildrenChanged()\",\r\n  \"OnTransformParentChanged()\",\r\n  \"OnValidate()\",\r\n  \"Reset()\",\r\n  \"OnServerConnect(NetworkConnection)\",\r\n  \"OnServerDisconnect(NetworkConnection)\",\r\n  \"OnServerReady(NetworkConnection)\",\r\n  \"OnServerAddPlayer(NetworkConnection,short)\",\r\n  \"OnServerRemovePlayer(NetworkConnection,PlayerController)\",\r\n  \"OnClientConnect(NetworkConnection)\",\r\n  \"OnClientDisconnect(NetworkConnection)\",\r\n  \"OnClientError(NetworkConnection,int)\",\r\n  \"OnClientNotReady()\",\r\n  \"OnClientSceneChanged(NetworkConnection)\",\r\n  \"OnServerInitialized()\",\r\n  \"OnConnectedToServer()\",\r\n  \"OnPlayerConnected(NetworkPlayer)\",\r\n  \"OnPlayerDisconnected(NetworkPlayer)\",\r\n  \"OnFailedToConnect(NetworkConnectionError)\",\r\n  \"OnFailedToConnectToMasterServer(NetworkConnectionError)\",\r\n  \"OnMasterServerEvent(MasterServerEvent)\",\r\n  \"OnNetworkInstantiate(NetworkMessageInfo)\",\r\n  \"OnSerializeNetworkView(BitStream,NetworkMessageInfo)\"\r\n]\r\n }";

        /// <summary>
        /// Gets the list of hooks as a strongly-typed collection.
        /// </summary>
        public static List<HookModel> GetHooks()
        {
            try
            {
                using var doc = JsonDocument.Parse(Json);
                var hooks = new List<HookModel>();

                foreach (var hook in doc.RootElement.GetProperty("hooks").EnumerateArray())
                {
                    var hookString = hook.GetString();
                    if (string.IsNullOrWhiteSpace(hookString))
                    {
                        continue;
                    }

                    hooks.Add(HooksUtils.ParseHookString(hookString));
                }

                return hooks;
            }
            catch (Exception ex)
            {
                throw new JsonException("Failed to parse hooks from JSON", ex);
            }
        }
    }
}

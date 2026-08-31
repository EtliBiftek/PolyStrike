using System;
using PolyStrike.Core;
using PolyStrike.Match;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkConnectionMenu : MonoBehaviour
    {
        private const float ConnectionEntityGracePeriod = 1.25f;

        private string address = "127.0.0.1";
        private bool connectionRequested;
        private bool onlineScenePrepared;
        private float connectionRequestedAt;
        private string statusKey = "network.status.ready";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<NetworkConnectionMenu>() != null)
                return;

            var root = new GameObject("PolyStrike Network");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkConnectionMenu>();
            root.AddComponent<NetworkClientPresentation>();
            root.AddComponent<NetworkBuyMenu>();
        }

        private void Update()
        {
            if (!connectionRequested || IsClientConnected())
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // A connect request is consumed before the connection entity appears, so give NetCode one short grace window.
            if (HasClientConnectionEntity() || Time.unscaledTime - connectionRequestedAt < ConnectionEntityGracePeriod)
                return;

            connectionRequested = false;
            statusKey = "network.status.failed";
        }

        private void OnGUI()
        {
            if (IsClientConnected())
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            const float width = 420f;
            const float height = 320f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, string.Empty);

            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            GUILayout.Label(Localization.Get("network.title"));
            GUILayout.Space(12f);

            if (!connectionRequested)
            {
                if (statusKey != "network.status.ready")
                {
                    GUILayout.Label(Localization.Get(statusKey));
                    GUILayout.Space(8f);
                }

                GUILayout.Label(Localization.Get("network.address"));
                address = GUILayout.TextField(address, 64);
                GUILayout.Space(10f);

                if (GUILayout.Button(Localization.Get("network.host"), GUILayout.Height(36f)))
                    StartHost();

                if (GUILayout.Button(Localization.Get("network.join"), GUILayout.Height(36f)))
                    Join(address);
            }
            else
            {
                GUILayout.Label(Localization.Get(statusKey));
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("network.port_hint").Replace("{0}", PolyStrikeNetcodeBootstrap.DefaultGamePort.ToString()));
            GUILayout.EndArea();
        }

        private void StartHost()
        {
            if (connectionRequested)
                return;

            var serverWorld = ClientServerBootstrap.ServerWorld;
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(serverWorld) || !IsUsable(clientWorld))
            {
                statusKey = "network.status.world_error";
                return;
            }

            PrepareOnlineScene();

            var listenEndpoint = NetworkEndpoint.AnyIpv4.WithPort(PolyStrikeNetcodeBootstrap.DefaultGamePort);
            var listen = serverWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestListen));
            serverWorld.EntityManager.SetComponentData(listen, new NetworkStreamRequestListen { Endpoint = listenEndpoint });

            var localEndpoint = NetworkEndpoint.LoopbackIpv4.WithPort(PolyStrikeNetcodeBootstrap.DefaultGamePort);
            var connect = clientWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientWorld.EntityManager.SetComponentData(connect, new NetworkStreamRequestConnect { Endpoint = localEndpoint });

            BeginConnectionAttempt("network.status.hosting");
        }

        private void Join(string host)
        {
            if (connectionRequested)
                return;

            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(clientWorld))
            {
                statusKey = "network.status.world_error";
                return;
            }

            NetworkEndpoint endpoint;
            try
            {
                endpoint = NetworkEndpoint.Parse(host.Trim(), PolyStrikeNetcodeBootstrap.DefaultGamePort);
            }
            catch (Exception)
            {
                statusKey = "network.status.invalid_address";
                return;
            }

            if (!endpoint.IsValid)
            {
                statusKey = "network.status.invalid_address";
                return;
            }

            PrepareOnlineScene();
            var connect = clientWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientWorld.EntityManager.SetComponentData(connect, new NetworkStreamRequestConnect { Endpoint = endpoint });
            BeginConnectionAttempt("network.status.connecting");
        }

        private void BeginConnectionAttempt(string nextStatusKey)
        {
            connectionRequested = true;
            connectionRequestedAt = Time.unscaledTime;
            statusKey = nextStatusKey;
        }

        private void PrepareOnlineScene()
        {
            if (onlineScenePrepared)
                return;

            onlineScenePrepared = true;

            var participants = FindObjectsByType<MatchParticipant>(FindObjectsSortMode.None);
            for (var i = 0; i < participants.Length; i++)
            {
                if (participants[i] != null)
                    Destroy(participants[i].gameObject);
            }

            var roundManager = FindFirstObjectByType<MatchRoundManager>();
            if (roundManager != null)
                Destroy(roundManager.gameObject);

            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                    listeners[i].enabled = false;
            }
        }

        private static bool IsClientConnected()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(clientWorld))
                return false;

            var query = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkId));
            var connected = query.CalculateEntityCount() > 0;
            query.Dispose();
            return connected;
        }

        private static bool HasClientConnectionEntity()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;
            if (!IsUsable(clientWorld))
                return false;

            var query = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));
            var exists = query.CalculateEntityCount() > 0;
            query.Dispose();
            return exists;
        }

        private static bool IsUsable(World world) => world != null && world.IsCreated;
    }
}

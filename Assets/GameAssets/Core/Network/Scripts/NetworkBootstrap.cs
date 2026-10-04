using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Friendslop.Core.Network
{
    /// <summary>
    /// Selects the transport inside Multipass and starts the connection for the resolved role.
    ///
    /// Transport resolution: -transport arg > editor (always Tugboat) > NetworkConfig.DefaultTransport.
    /// Role resolution: -netrole arg > editor (MPPM tag "Host"/"Server"/"Client", else main editor = Host, virtual player = Client) > NetworkConfig.DefaultBuildRole.
    /// Other args: -address, -port.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkConfig _config;

        private NetworkManager _networkManager;
        private Multipass _multipass;

        public TransportKind ActiveTransport { get; private set; }
        public NetworkRole Role { get; private set; }

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();
            _multipass = _networkManager.TransportManager.GetTransport<Multipass>();
            if (_multipass == null)
            {
                Debug.LogError("[NetworkBootstrap] TransportManager must use Multipass.");
                return;
            }

            ActiveTransport = ResolveTransport();
            if (GetTransport(ActiveTransport) == null)
            {
                Debug.LogWarning($"[NetworkBootstrap] Transport {ActiveTransport} not available, falling back to Tugboat.");
                ActiveTransport = TransportKind.Tugboat;
            }
            Role = ResolveRole();
            Debug.Log($"[NetworkBootstrap] Transport={ActiveTransport} Role={Role}");

            if (Role != NetworkRole.None)
                StartAs(Role);
        }

        public void StartAs(NetworkRole role)
        {
            Transport transport = GetTransport(ActiveTransport);
            if (transport == null)
            {
                Debug.LogError($"[NetworkBootstrap] Transport {ActiveTransport} not found in Multipass.");
                return;
            }

            if (transport is Tugboat)
            {
                transport.SetPort(ReadUShortArg("-port") ?? _config.Port);
                transport.SetClientAddress(ReadArg("-address") ?? _config.Address);
            }

            _multipass.SetClientTransport(transport);

            if (role == NetworkRole.Host || role == NetworkRole.Server)
                _multipass.StartConnection(true, transport.Index);
            if (role == NetworkRole.Host || role == NetworkRole.Client)
                _networkManager.ClientManager.StartConnection();
        }

        private Transport GetTransport(TransportKind kind)
        {
            return kind switch
            {
                TransportKind.Tugboat => _multipass.GetTransport<Tugboat>(),
                // TODO: FishySteamworks.
                _ => null
            };
        }

        private TransportKind ResolveTransport()
        {
            string arg = ReadArg("-transport");
            if (arg != null && Enum.TryParse(arg, true, out TransportKind fromArg))
                return fromArg;
#if UNITY_EDITOR
            return TransportKind.Tugboat;
#else
            return _config.DefaultTransport;
#endif
        }

        private NetworkRole ResolveRole()
        {
            string arg = ReadArg("-netrole");
            if (arg != null && Enum.TryParse(arg, true, out NetworkRole fromArg))
                return fromArg;
#if UNITY_EDITOR
            if (!_config.AutoStartInEditor)
                return NetworkRole.None;
            foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.Tags)
            {
                if (Enum.TryParse(tag, true, out NetworkRole fromTag))
                    return fromTag;
            }
            return Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor ? NetworkRole.Host : NetworkRole.Client;
#else
            return _config.DefaultBuildRole;
#endif
        }

        private static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(name.Length + 1);
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            }
            return null;
        }

        private static ushort? ReadUShortArg(string name)
        {
            string value = ReadArg(name);
            return value != null && ushort.TryParse(value, out ushort result) ? result : null;
        }
    }
}

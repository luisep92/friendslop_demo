using UnityEngine;

namespace Friendslop.Core.Network
{
    public enum TransportKind
    {
        Tugboat,
        Steam
    }

    public enum NetworkRole
    {
        None,
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Project-wide network defaults. Command-line args override these at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Friendslop/Network Config")]
    public sealed class NetworkConfig : ScriptableObject
    {
        [Tooltip("Transport used in builds when no -transport arg is given. Editor always uses Tugboat.")]
        public TransportKind DefaultTransport = TransportKind.Tugboat;

        [Tooltip("Role used in builds when no -netrole arg is given.")]
        public NetworkRole DefaultBuildRole = NetworkRole.None;

        [Header("Editor")]
        [Tooltip("Auto start in Play Mode. Main editor = Host, MPPM virtual players = Client, unless overridden by MPPM tags.")]
        public bool AutoStartInEditor = true;

        [Header("Tugboat")]
        public string Address = "localhost";
        public ushort Port = 7770;
    }
}

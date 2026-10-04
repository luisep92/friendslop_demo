using FishNet;
using Friendslop.Core.Network;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// IMGUI debug panel and debug keys. Not networked, so F9 works while disconnected.
    /// F5 demo layout, F6 save, F7 load (server); F8 corrupt replica (client); F9 join as client, F10 leave (client).
    /// </summary>
    public sealed class FactoryDebugOverlay : MonoBehaviour
    {
        private const float MessageSeconds = 5f;

        private InputAction _demo;
        private InputAction _save;
        private InputAction _load;
        private InputAction _corrupt;
        private InputAction _join;
        private InputAction _leave;
        private GUIStyle _style;

        private void OnEnable()
        {
            _demo = Create("DebugDemo", "<Keyboard>/f5");
            _save = Create("DebugSave", "<Keyboard>/f6");
            _load = Create("DebugLoad", "<Keyboard>/f7");
            _corrupt = Create("DebugCorrupt", "<Keyboard>/f8");
            _join = Create("DebugJoin", "<Keyboard>/f9");
            _leave = Create("DebugLeave", "<Keyboard>/f10");
        }

        private void OnDisable()
        {
            _demo.Dispose();
            _save.Dispose();
            _load.Dispose();
            _corrupt.Dispose();
            _join.Dispose();
            _leave.Dispose();
        }

        private void Update()
        {
            FactoryNetwork network = FactoryNetwork.Instance;
            bool isServer = InstanceFinder.IsServerStarted;
            bool isClient = InstanceFinder.IsClientStarted;

            if (_demo.WasPressedThisFrame() && network != null)
                network.RequestDemoLayout();

            if (_save.WasPressedThisFrame() && network != null)
                network.SaveToDisk();

            if (_load.WasPressedThisFrame() && network != null)
                network.LoadFromDisk();

            if (_corrupt.WasPressedThisFrame() && network != null)
                network.DebugCorruptReplica();

            if (_join.WasPressedThisFrame() && !isServer && !isClient)
            {
                NetworkBootstrap bootstrap = FindFirstObjectByType<NetworkBootstrap>();
                if (bootstrap != null)
                    bootstrap.StartAs(NetworkRole.Client);
            }

            if (_leave.WasPressedThisFrame() && isClient && !isServer)
                InstanceFinder.ClientManager.StopConnection();
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
            GUILayout.BeginArea(new Rect(10, 10, 560, 400));
            GUILayout.Box(BuildText(), _style);
            GUILayout.EndArea();
        }

        private static string BuildText()
        {
            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            var text = new System.Text.StringBuilder();

            text.AppendLine($"<b>Role</b> {Role()}");
            if (sim != null)
                text.AppendLine($"<b>Tick</b> {sim.Tick}   <b>Coins</b> {sim.Coins}   <b>Buildings</b> {sim.Buildings.Count}");
            if (network != null && network.PersistenceMessage != null && Time.unscaledTime - network.PersistenceMessageTime < MessageSeconds)
                text.AppendLine($"<b>Save</b> {network.PersistenceMessage}");

            LockstepClient replica = network != null ? network.Replica : null;
            if (replica != null)
            {
                long rtt = InstanceFinder.TimeManager != null ? InstanceFinder.TimeManager.RoundTripTime : 0;
                text.AppendLine($"<b>Net</b> buffered {replica.Buffered}   behind {replica.LastReceivedTick - replica.Sim.Tick}   RTT {rtt} ms");
                string status = replica.NeedsResync ? "<color=orange>RESYNCING</color>" : "<color=lime>OK</color>";
                text.AppendLine($"<b>Checksum</b> {status}   ok {replica.ChecksumsOk}   mismatch {replica.Mismatches}   resync {replica.Resyncs}");
            }

            BuildTool tool = BuildTool.Local;
            if (tool != null)
            {
                if (tool.Mode == BuildMode.Dismantle)
                {
                    string progress = tool.DismantleProgress > 0f ? $"   dismantling {tool.DismantleProgress:P0}" : string.Empty;
                    text.AppendLine($"<b>Mode</b> <color=red>DISMANTLE</color>   marked {tool.SelectionCount}{progress}");
                }
                else if (tool.Mode == BuildMode.None)
                {
                    text.AppendLine("<b>Mode</b> none");
                }
                else
                    text.AppendLine($"<b>Mode</b> build [{tool.SelectedSlot + 1}] {tool.SelectedDef.Name}   rot {tool.Rotation} ({Footprint.Forward(tool.Rotation)})");

                if (tool.BeltRunLength > 0)
                {
                    string state = tool.BeltRunValid ? "<color=lime>ok</color>" : "<color=red>blocked</color>";
                    text.AppendLine($"<b>Belt run</b> {tool.BeltRunLength} tiles {state}");
                }
                if (tool.PendingCount > 0)
                    text.AppendLine($"<b>Pending</b> {tool.PendingCount}");
                text.AppendLine($"<b>Target</b> {(tool.TargetBuilding != null ? tool.TargetBuilding.Describe() : "-")}");
            }

            text.AppendLine();
            text.AppendLine("1-6 build   LMB place (belts: start, end)   R rotate / flip corner / turn end   RMB back / exit mode");
            text.AppendLine("F dismantle: click mark, Ctrl sweep, hold LMB dismantle   MMB sample   E inspect   Tab cursor");
            text.Append("Server: F5 demo line, F6 save, F7 load   Client: F8 corrupt, F9 join, F10 leave");
            return text.ToString();
        }

        private static string Role()
        {
            bool isServer = InstanceFinder.IsServerStarted;
            bool isClient = InstanceFinder.IsClientStarted;
            if (isServer && isClient)
                return "Host";
            if (isServer)
                return "Server";
            return isClient ? "Client" : "Offline";
        }

        private static InputAction Create(string name, string binding)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.Enable();
            return action;
        }
    }
}

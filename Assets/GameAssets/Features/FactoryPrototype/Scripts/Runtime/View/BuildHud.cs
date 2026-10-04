using System.Collections.Generic;
using System.Text;
using Friendslop.Features.FactoryPrototype.Simulation;
using UnityEngine;

namespace Friendslop.Features.FactoryPrototype
{
    /// <summary>
    /// Local player HUD (IMGUI for the prototype): crosshair, placement rejection reason, and the
    /// inspector panel opened with E: recipe, buffers, progress, max and measured rate.
    /// </summary>
    public sealed class BuildHud : MonoBehaviour
    {
        private const int RateWindowTicks = 30 * SimConstants.TicksPerSecond;
        private const float PanelWidth = 340f;

        private readonly Queue<Sample> _samples = new Queue<Sample>();
        private Sample _last;
        private int _sampledId;
        private GUIStyle _label;
        private GUIStyle _message;
        private GUIStyle _title;

        private void Update()
        {
            BuildTool tool = BuildTool.Local;
            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            Building building = tool != null && sim != null ? sim.GetBuilding(tool.InspectedBuildingId) : null;
            if (building == null)
            {
                _samples.Clear();
                _sampledId = 0;
                return;
            }

            // New target, or the tick went backwards (save loaded): restart measuring.
            if (building.Id != _sampledId || (_samples.Count > 0 && sim.Tick < _last.Tick))
            {
                _samples.Clear();
                _sampledId = building.Id;
            }

            if (_samples.Count == 0 || _last.Tick != sim.Tick)
            {
                _last = new Sample { Tick = sim.Tick, Count = building.Throughput };
                _samples.Enqueue(_last);
            }
            while (_samples.Count > 1 && sim.Tick - _samples.Peek().Tick > RateWindowTicks)
                _samples.Dequeue();
        }

        private void OnGUI()
        {
            BuildTool tool = BuildTool.Local;
            if (tool == null)
                return;

            EnsureStyles();
            DrawCrosshair(tool);

            FactoryNetwork network = FactoryNetwork.Instance;
            FactorySim sim = network != null ? network.ActiveSim : null;
            Building building = sim != null ? sim.GetBuilding(tool.InspectedBuildingId) : null;
            if (building != null)
                DrawInspector(sim, building);
        }

        private void DrawCrosshair(BuildTool tool)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), Texture2D.whiteTexture);

            if (tool.PlacementMessage != null)
                GUI.Label(new Rect(center.x - 200f, center.y + 14f, 400f, 24f), tool.PlacementMessage, _message);
        }

        private void DrawInspector(FactorySim sim, Building building)
        {
            GUILayout.BeginArea(new Rect(Screen.width - PanelWidth - 10f, 10f, PanelWidth, Screen.height - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label($"{building.Def.Name} #{building.Id}", _title);
            if (building.Status != BuildingStatus.None)
                GUILayout.Label($"Status: <color=#{ColorUtility.ToHtmlStringRGB(FactoryPalette.Status(building.Status))}>{building.Status}</color>", _label);

            switch (building)
            {
                case CrafterBuilding crafter:
                    DrawCrafter(sim, crafter);
                    break;
                case SourceBuilding source:
                    float seconds = (float)source.Def.SourceIntervalTicks / SimConstants.TicksPerSecond;
                    GUILayout.Label($"Produces 1 {ItemName(sim, source.Def.SourceItem)} every {seconds:0.##} s", _label);
                    GUILayout.Label($"Max: {60f / seconds:0.#}/min", _label);
                    break;
                case SinkBuilding sink:
                    GUILayout.Label($"Sold: {sink.Delivered}   Coins: {sim.Coins}", _label);
                    break;
                case BeltBuilding belt:
                    GUILayout.Label($"Items: {belt.Items.Count}   facing {belt.Forward}", _label);
                    break;
            }

            if (building.Status != BuildingStatus.None)
                GUILayout.Label($"Measured: {MeasuredRate()}", _label);

            GUILayout.Space(4f);
            GUILayout.Label("<i>E to close</i>", _label);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawCrafter(FactorySim sim, CrafterBuilding crafter)
        {
            RecipeDef recipe = crafter.Recipe;
            float seconds = (float)recipe.DurationTicks / SimConstants.TicksPerSecond;

            var text = new StringBuilder();
            for (int i = 0; i < recipe.Inputs.Count; i++)
                text.Append(i == 0 ? "" : " + ").Append($"{recipe.Inputs[i].Count} {ItemName(sim, recipe.Inputs[i].Item)}");
            text.Append("  →  ");
            for (int i = 0; i < recipe.Outputs.Count; i++)
                text.Append(i == 0 ? "" : " + ").Append($"{recipe.Outputs[i].Count} {ItemName(sim, recipe.Outputs[i].Item)}");
            GUILayout.Label($"Recipe: {text}  ({seconds:0.##} s)", _label);
            GUILayout.Label($"Max: {60f / seconds:0.#} crafts/min", _label);

            float progress = crafter.State == CrafterState.Crafting ? (float)crafter.Progress / recipe.DurationTicks : 0f;
            Bar("Progress", progress, FactoryPalette.Status(BuildingStatus.Working));

            GUILayout.Label("<b>Inputs</b>", _label);
            for (int i = 0; i < recipe.Inputs.Count; i++)
                Buffer(ItemName(sim, recipe.Inputs[i].Item), crafter.GetInputCount(i), FactoryPalette.Item(recipe.Inputs[i].Item));

            GUILayout.Label("<b>Outputs</b>", _label);
            for (int i = 0; i < recipe.Outputs.Count; i++)
                Buffer(ItemName(sim, recipe.Outputs[i].Item), crafter.GetOutputCount(i), FactoryPalette.Item(recipe.Outputs[i].Item));
        }

        private void Buffer(string name, int count, Color color)
        {
            Bar($"{name} {count}/{SimConstants.MachineBufferCapacity}", (float)count / SimConstants.MachineBufferCapacity, color);
        }

        private void Bar(string label, float fill, Color color)
        {
            GUILayout.Label(label, _label);
            Rect rect = GUILayoutUtility.GetRect(PanelWidth - 30f, 10f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.5f), 0f, 0f);
            rect.width *= Mathf.Clamp01(fill);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);
        }

        private string MeasuredRate()
        {
            if (_samples.Count < 2)
                return "measuring...";

            Sample first = _samples.Peek();
            float seconds = (float)(_last.Tick - first.Tick) / SimConstants.TicksPerSecond;
            if (seconds < 1f)
                return "measuring...";

            return $"{(_last.Count - first.Count) / seconds * 60f:0.#}/min (last {seconds:0} s)";
        }

        private static string ItemName(FactorySim sim, ushort id) => sim.Content.GetItem(id)?.Name ?? id.ToString();

        private void EnsureStyles()
        {
            if (_label != null)
                return;

            _label = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, wordWrap = true };
            _title = new GUIStyle(_label) { fontSize = 17, fontStyle = FontStyle.Bold };
            _message = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = FactoryPalette.Status(BuildingStatus.Blocked) }
            };
        }

        private struct Sample
        {
            public int Tick;
            public int Count;
        }
    }
}

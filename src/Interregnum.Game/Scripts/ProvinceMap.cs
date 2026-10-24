using Godot;
using Interregnum.Core.Scenarios;

namespace Interregnum.Game;

public sealed partial class ProvinceMap : Control
{
    private readonly Dictionary<string, Color> _factionColors = new(StringComparer.Ordinal);
    private IReadOnlyList<ProvinceDefinition> _provinces = [];
    private string? _selectedId;

    public event Action<ProvinceDefinition>? ProvinceSelected;

    public void LoadScenario(ScenarioDefinition scenario)
    {
        _provinces = scenario.Provinces;
        _factionColors.Clear();
        foreach (var faction in scenario.Factions)
        {
            _factionColors[faction.Id] = Color.FromHtml(faction.Color);
        }

        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
        {
            return;
        }

        foreach (var province in _provinces.Reverse())
        {
            var polygon = ToVectors(province);
            if (!ContainsPoint(polygon, mouse.Position)) continue;

            _selectedId = province.Id;
            ProvinceSelected?.Invoke(province);
            QueueRedraw();
            AcceptEvent();
            return;
        }
    }

    public override void _Draw()
    {
        foreach (var province in _provinces)
        {
            var polygon = ToVectors(province);
            var color = _factionColors.GetValueOrDefault(province.OwnerId, Colors.DimGray);
            DrawColoredPolygon(polygon, color.Darkened(0.12f));

            var border = string.Equals(_selectedId, province.Id, StringComparison.Ordinal)
                ? new Color("f3d57b")
                : new Color(0.10f, 0.12f, 0.13f, 1f);
            var width = string.Equals(_selectedId, province.Id, StringComparison.Ordinal) ? 5f : 2f;
            DrawPolyline([.. polygon, polygon[0]], border, width, true);

            var center = polygon.Aggregate(Vector2.Zero, (sum, point) => sum + point) / polygon.Length;
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-85, 5), province.Name,
                HorizontalAlignment.Center, 170, 15, Colors.White);
        }
    }

    private static Vector2[] ToVectors(ProvinceDefinition province) =>
        province.MapPolygon.Select(point => new Vector2(point.X, point.Y)).ToArray();

    private static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        var inside = false;
        for (var current = 0; current < polygon.Count; current++)
        {
            var previous = current == 0 ? polygon.Count - 1 : current - 1;
            var a = polygon[current];
            var b = polygon[previous];
            var crosses = (a.Y > point.Y) != (b.Y > point.Y)
                && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
            if (crosses) inside = !inside;
        }

        return inside;
    }
}

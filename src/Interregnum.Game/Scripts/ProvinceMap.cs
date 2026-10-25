using System.Text.Json;
using Godot;
using Interregnum.Core.Scenarios;

namespace Interregnum.Game;

public sealed partial class ProvinceMap : Control
{
    private const float DesignWidth = 874f;
    private const float DesignHeight = 580f;
    private const float MinimumZoom = 1f;
    private const float MaximumZoom = 3.5f;
    private static readonly Color OceanColor = new("17262b");
    private static readonly Color LandColor = new("3a4038");
    private static readonly Color ContextLabelColor = new(0.62f, 0.64f, 0.57f, 0.72f);

    private const double MinimumLongitude = -12d;
    private const double MaximumLongitude = 20d;
    private const double MinimumLatitude = 35d;
    private const double MaximumLatitude = 61d;

    private readonly Dictionary<string, Color> _factionColors = new(StringComparer.Ordinal);
    private readonly List<Vector2[]> _contextLandPolygons = [];
    private readonly List<Vector2[]> _gaulLandCandidates = [];
    private readonly Dictionary<string, IReadOnlyList<Vector2[]>> _provincePolygons = new(StringComparer.Ordinal);
    private IReadOnlyList<ProvinceDefinition> _provinces = [];
    private string? _selectedId;
    private Vector2 _pan;
    private float _zoom = MinimumZoom;
    private bool _isPanning;

    public event Action<ProvinceDefinition>? ProvinceSelected;

    public void LoadScenario(ScenarioDefinition scenario)
    {
        LoadContextGeography("res://Data/western-europe-land.geojson");
        _provinces = scenario.Provinces;
        _factionColors.Clear();
        foreach (var faction in scenario.Factions)
        {
            _factionColors[faction.Id] = Color.FromHtml(faction.Color);
        }

        BuildProvincePolygons();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                ZoomAt(mouseButton.Position, 1.2f);
                AcceptEvent();
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                ZoomAt(mouseButton.Position, 1f / 1.2f);
                AcceptEvent();
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isPanning = mouseButton.Pressed;
                AcceptEvent();
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                SelectProvince(ScreenToMap(mouseButton.Position));
                return;
            }
        }

        if (inputEvent is InputEventMouseMotion mouseMotion && _isPanning)
        {
            _pan += mouseMotion.Relative;
            ClampPan();
            QueueRedraw();
            AcceptEvent();
        }
    }

    public void ZoomIn() => ZoomAt(Size / 2f, 1.25f);

    public void ZoomOut() => ZoomAt(Size / 2f, 1f / 1.25f);

    public void ResetView()
    {
        _zoom = MinimumZoom;
        _pan = Vector2.Zero;
        QueueRedraw();
    }

    private void SelectProvince(Vector2 mapPosition)
    {
        foreach (var province in _provinces.Reverse())
        {
            if (!_provincePolygons[province.Id].Any(polygon => ContainsPoint(polygon, mapPosition))) continue;

            _selectedId = province.Id;
            ProvinceSelected?.Invoke(province);
            QueueRedraw();
            AcceptEvent();
            return;
        }
    }

    public override void _Draw()
    {
        DrawSetTransform(_pan, 0f, new Vector2(_zoom, _zoom));
        DrawRect(new Rect2(Vector2.Zero, Size), OceanColor);
        DrawContextGeography();

        foreach (var province in _provinces)
        {
            var color = _factionColors.GetValueOrDefault(province.OwnerId, Colors.DimGray);
            var border = string.Equals(_selectedId, province.Id, StringComparison.Ordinal)
                ? new Color("f3d57b")
                : new Color(0.09f, 0.11f, 0.10f, 0.92f);
            var width = string.Equals(_selectedId, province.Id, StringComparison.Ordinal) ? 5f : 2f;

            foreach (var polygon in _provincePolygons[province.Id])
            {
                DrawColoredPolygon(polygon, color.Darkened(0.18f));
                DrawPolyline([.. polygon, polygon[0]], border, width, true);
            }

            var labelPolygon = _provincePolygons[province.Id].MaxBy(PolygonArea)!;
            var center = labelPolygon.Aggregate(Vector2.Zero, (sum, point) => sum + point) / labelPolygon.Length;
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-70, 5), province.Name,
                HorizontalAlignment.Center, 140, 14, Colors.White);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(14, 25), "WESTERN EUROPE  •  480 AD",
            HorizontalAlignment.Left, -1, 13, new Color("c9bb96"));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawContextGeography()
    {
        foreach (var normalizedPolygon in _contextLandPolygons)
        {
            var polygon = normalizedPolygon
                .Select(point => new Vector2(point.X * Size.X, point.Y * Size.Y))
                .ToArray();
            DrawColoredPolygon(polygon, LandColor);
        }

        DrawContextLabel("BRITANNIA", Project(-4.5d, 55d));
        DrawContextLabel("HISPANIA", Project(-4.5d, 39.5d));
        DrawContextLabel("GERMANIA", Project(10d, 51d));
        DrawContextLabel("ITALIA", Project(12d, 42d));
        DrawContextLabel("AFRICA", Project(2d, 36.2d));
        DrawContextLabel("ATLANTIC", Project(-11.5d, 47d));
        DrawContextLabel("MARE INTERNUM", Project(7d, 37.4d));
        DrawString(ThemeDB.FallbackFont, new Vector2(Size.X - 218, Size.Y - 8),
            "Coastline: Natural Earth • public domain", HorizontalAlignment.Left, -1, 9,
            new Color(0.62f, 0.64f, 0.57f, 0.55f));
    }

    private void DrawContextLabel(string text, Vector2 position) =>
        DrawString(ThemeDB.FallbackFont, position, text, HorizontalAlignment.Left, -1, 11, ContextLabelColor);

    private void LoadContextGeography(string resourcePath)
    {
        _contextLandPolygons.Clear();
        _gaulLandCandidates.Clear();

        try
        {
            var json = Godot.FileAccess.GetFileAsString(resourcePath);
            using var document = JsonDocument.Parse(json);
            foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
            {
                var sourceCode = feature.GetProperty("properties").GetProperty("sourceCode").GetString();
                var geometry = feature.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates");
                switch (geometry.GetProperty("type").GetString())
                {
                    case "Polygon":
                        AddOuterRing(coordinates, sourceCode);
                        break;
                    case "MultiPolygon":
                        foreach (var polygon in coordinates.EnumerateArray())
                        {
                            AddOuterRing(polygon, sourceCode);
                        }
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load contextual map data '{resourcePath}': {exception.Message}");
        }
    }

    private void AddOuterRing(JsonElement polygonCoordinates, string? sourceCode)
    {
        var points = polygonCoordinates[0]
            .EnumerateArray()
            .Select(coordinate => Normalize(coordinate[0].GetDouble(), coordinate[1].GetDouble()))
            .ToList();

        if (points.Count > 1 && points[0].IsEqualApprox(points[^1]))
        {
            points.RemoveAt(points.Count - 1);
        }

        var overlapsViewport = points.Count >= 3
            && points.Max(point => point.X) >= 0f
            && points.Min(point => point.X) <= 1f
            && points.Max(point => point.Y) >= 0f
            && points.Min(point => point.Y) <= 1f;
        if (overlapsViewport)
        {
            var polygon = points.ToArray();
            _contextLandPolygons.Add(polygon);
            if (string.Equals(sourceCode, "FRA", StringComparison.Ordinal))
            {
                _gaulLandCandidates.Add(polygon);
            }
        }
    }

    private void BuildProvincePolygons()
    {
        _provincePolygons.Clear();
        if (_gaulLandCandidates.Count == 0)
        {
            foreach (var province in _provinces)
            {
                _provincePolygons[province.Id] = [ToVectors(province)];
            }

            return;
        }

        // The reduced source also contains French islands. The largest European
        // polygon is mainland Gaul; internal provinces are clipped to its real coast.
        var gaulLand = _gaulLandCandidates.MaxBy(PolygonArea)!
            .Select(point => new Vector2(point.X * Size.X, point.Y * Size.Y))
            .ToArray();

        foreach (var province in _provinces)
        {
            var clipped = Geometry2D.IntersectPolygons(ToVectors(province), gaulLand)
                .Where(polygon => polygon.Length >= 3 && !Geometry2D.IsPolygonClockwise(polygon))
                .ToArray();
            _provincePolygons[province.Id] = clipped.Length > 0 ? clipped : [ToVectors(province)];
        }
    }

    private static float PolygonArea(IReadOnlyList<Vector2> polygon)
    {
        var twiceArea = 0f;
        for (var index = 0; index < polygon.Count; index++)
        {
            var next = (index + 1) % polygon.Count;
            twiceArea += (polygon[index].X * polygon[next].Y) - (polygon[next].X * polygon[index].Y);
        }

        return Mathf.Abs(twiceArea) / 2f;
    }

    private static Vector2 Normalize(double longitude, double latitude) => new(
        (float)((longitude - MinimumLongitude) / (MaximumLongitude - MinimumLongitude)),
        (float)((MaximumLatitude - latitude) / (MaximumLatitude - MinimumLatitude)));

    private Vector2 Project(double longitude, double latitude)
    {
        var normalized = Normalize(longitude, latitude);
        return new Vector2(normalized.X * Size.X, normalized.Y * Size.Y);
    }

    private Vector2[] ToVectors(ProvinceDefinition province) =>
        province.MapPolygon
            .Select(point => new Vector2(point.X * Size.X / DesignWidth, point.Y * Size.Y / DesignHeight))
            .ToArray();

    private Vector2 ScreenToMap(Vector2 screenPosition) => (screenPosition - _pan) / _zoom;

    private void ZoomAt(Vector2 screenPosition, float factor)
    {
        var mapPosition = ScreenToMap(screenPosition);
        _zoom = Mathf.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        _pan = screenPosition - (mapPosition * _zoom);
        ClampPan();
        QueueRedraw();
    }

    private void ClampPan()
    {
        var minimum = Size * (1f - _zoom);
        _pan = new Vector2(
            Mathf.Clamp(_pan.X, minimum.X, 0f),
            Mathf.Clamp(_pan.Y, minimum.Y, 0f));
    }

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

using System.Text.Json;
using Godot;
using Interregnum.Core.Domain;
using Interregnum.Core.Scenarios;

namespace Interregnum.Game;

public sealed partial class ProvinceMap : Control
{
    private const float DesignWidth = 874f;
    private const float DesignHeight = 580f;
    private const float MinimumZoom = 1f;
    private const float MaximumZoom = 6f;
    private const float DetailedLabelsZoom = 1.7f;
    private const float ArmyDetailZoom = 2.35f;

    private const double MinimumLongitude = -12d;
    private const double MaximumLongitude = 20d;
    private const double MinimumLatitude = 35d;
    private const double MaximumLatitude = 61d;

    private static readonly Color OceanColor = new("17262b");
    private static readonly Color FallbackLandColor = new("59604f");
    private static readonly Color ContextLabelColor = new(0.72f, 0.72f, 0.64f, 0.68f);
    private static readonly Color RiverColor = new(0.26f, 0.46f, 0.52f, 0.85f);

    private static readonly (string Name, (double Longitude, double Latitude)[] Points)[] Rivers =
    [
        ("Rhenus", [(8.2d, 47.6d), (7.8d, 48.6d), (7.6d, 49.5d), (7.3d, 50.3d), (6.5d, 51.2d), (5.9d, 51.8d), (4.8d, 52d)]),
        ("Rhodanus", [(6.1d, 46.2d), (5.2d, 45.6d), (4.8d, 44.9d), (4.8d, 43.7d)]),
        ("Liger", [(4d, 44.9d), (3d, 46d), (1.5d, 47d), (-0.5d, 47.3d), (-2.2d, 47.2d)]),
        ("Sequana", [(4.5d, 47.9d), (3.2d, 48.4d), (2.3d, 48.85d), (1d, 49.3d), (0.1d, 49.45d)]),
        ("Garumna", [(1.5d, 42.8d), (1.2d, 43.6d), (0.2d, 44.1d), (-0.6d, 44.5d), (-1.1d, 45d)])
    ];

    private readonly Dictionary<string, Color> _factionColors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownerByProvinceId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _armyStrengthByProvinceId = new(StringComparer.Ordinal);
    private readonly List<Vector2[]> _contextLandPolygons = [];
    private readonly List<Vector2[]> _gaulLandCandidates = [];
    private readonly Dictionary<string, IReadOnlyList<Vector2[]>> _provincePolygons = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rebellingProvinceIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _atRiskProvinceIds = new(StringComparer.Ordinal);
    private IReadOnlyList<ProvinceDefinition> _provinces = [];
    private Texture2D? _terrainTexture;
    private string? _selectedId;
    private Vector2 _pan;
    private float _zoom = MinimumZoom;
    private bool _isPanning;

    public event Action<ProvinceDefinition>? ProvinceSelected;

    public void LoadScenario(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        LoadContextGeography("res://Data/western-europe-land.geojson");
        _terrainTexture = LoadTexture("res://Assets/western-europe-terrain.png");
        _provinces = scenario.Provinces;
        _factionColors.Clear();
        _ownerByProvinceId.Clear();
        foreach (var faction in scenario.Factions)
        {
            _factionColors[faction.Id] = Color.FromHtml(faction.Color);
        }

        foreach (var province in scenario.Provinces)
        {
            _ownerByProvinceId[province.Id] = province.OwnerId;
        }

        BuildProvincePolygons();
        QueueRedraw();
    }

    public void ApplyWorldState(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _ownerByProvinceId.Clear();
        _rebellingProvinceIds.Clear();
        _atRiskProvinceIds.Clear();
        _armyStrengthByProvinceId.Clear();
        foreach (var province in world.Provinces.Values)
        {
            _ownerByProvinceId[province.Id.Value] = province.OwnerId.Value;
            if (province.IsRebelling)
            {
                _rebellingProvinceIds.Add(province.Id.Value);
            }
            else if (province.IsAtRisk)
            {
                _atRiskProvinceIds.Add(province.Id.Value);
            }
        }

        foreach (var army in world.Armies.Values)
        {
            _armyStrengthByProvinceId[army.ProvinceId.Value] =
                _armyStrengthByProvinceId.GetValueOrDefault(army.ProvinceId.Value) + army.Strength;
        }

        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationResized || Size.X <= 0f || Size.Y <= 0f) return;

        BuildProvincePolygons();
        ClampPan();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed) GrabFocus();

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
            PanBy(mouseMotion.Relative);
            AcceptEvent();
            return;
        }

        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.Keycode)
            {
                case Key.Plus:
                case Key.Equal:
                case Key.KpAdd:
                    ZoomIn();
                    break;
                case Key.Minus:
                case Key.KpSubtract:
                    ZoomOut();
                    break;
                case Key.Key0:
                case Key.Home:
                    ResetView();
                    break;
                case Key.Left:
                case Key.A:
                    PanBy(new Vector2(48f, 0f));
                    break;
                case Key.Right:
                case Key.D:
                    PanBy(new Vector2(-48f, 0f));
                    break;
                case Key.Up:
                case Key.W:
                    PanBy(new Vector2(0f, 48f));
                    break;
                case Key.Down:
                case Key.S:
                    PanBy(new Vector2(0f, -48f));
                    break;
                default:
                    return;
            }

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

    public override void _Draw()
    {
        DrawOcean();
        DrawSetTransform(_pan, 0f, new Vector2(_zoom, _zoom));
        DrawContextGeography();
        DrawRivers();
        DrawProvinceOverlays();
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawMapLabelsAndMarkers();
        DrawMapChrome();
    }

    private void DrawOcean()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), OceanColor);
        const int bands = 9;
        for (var index = 0; index < bands; index++)
        {
            var y = Size.Y * index / bands;
            var color = index % 2 == 0
                ? new Color(0.08f, 0.16f, 0.18f, 0.1f)
                : new Color(0.02f, 0.07f, 0.09f, 0.08f);
            DrawRect(new Rect2(0f, y, Size.X, Size.Y / bands), color);
        }
    }

    private void DrawContextGeography()
    {
        if (_terrainTexture is not null)
        {
            DrawTextureRect(_terrainTexture, new Rect2(Vector2.Zero, Size), false);
            return;
        }

        foreach (var normalizedPolygon in _contextLandPolygons)
        {
            var polygon = normalizedPolygon.Select(ToMapPoint).ToArray();
            DrawColoredPolygon(polygon, FallbackLandColor);
        }
    }

    private void DrawRivers()
    {
        foreach (var (_, river) in Rivers)
        {
            var points = river.Select(point => Project(point.Longitude, point.Latitude)).ToArray();
            DrawPolyline(points, new Color(0.06f, 0.12f, 0.14f, 0.55f), 3.2f / _zoom, true);
            DrawPolyline(points, RiverColor, 1.35f / _zoom, true);
        }
    }

    private void DrawProvinceOverlays()
    {
        foreach (var province in _provinces)
        {
            if (!_provincePolygons.TryGetValue(province.Id, out var polygons)) continue;

            var ownerId = _ownerByProvinceId.GetValueOrDefault(province.Id, province.OwnerId);
            var factionColor = _factionColors.GetValueOrDefault(ownerId, Colors.DimGray);
            var ownershipTint = new Color(factionColor.R, factionColor.G, factionColor.B, 0.24f);
            var isSelected = string.Equals(_selectedId, province.Id, StringComparison.Ordinal);
            var isRebelling = _rebellingProvinceIds.Contains(province.Id);
            var isAtRisk = _atRiskProvinceIds.Contains(province.Id);
            var border = isSelected
                ? new Color("f3d57b")
                : isRebelling
                    ? new Color("df735d")
                    : isAtRisk
                        ? new Color("d5aa60")
                        : new Color(0.11f, 0.13f, 0.12f, 0.66f);
            var screenWidth = isSelected ? 3.2f : isRebelling || isAtRisk ? 2.2f : 1.15f;

            foreach (var polygon in polygons)
            {
                DrawColoredPolygon(polygon, ownershipTint);
                if (isRebelling)
                {
                    DrawColoredPolygon(polygon, new Color(0.62f, 0.1f, 0.06f, 0.3f));
                }
                else if (isAtRisk)
                {
                    DrawColoredPolygon(polygon, new Color(0.61f, 0.4f, 0.07f, 0.17f));
                }

                DrawPolyline([.. polygon, polygon[0]], border, screenWidth / _zoom, true);
            }
        }
    }

    private void DrawMapLabelsAndMarkers()
    {
        foreach (var province in _provinces)
        {
            if (!_provincePolygons.TryGetValue(province.Id, out var polygons) || polygons.Count == 0) continue;

            var labelPolygon = polygons.MaxBy(PolygonArea)!;
            var center = MapToScreen(Centroid(labelPolygon));
            if (!new Rect2(Vector2.Zero, Size).HasPoint(center)) continue;

            var label = _zoom < DetailedLabelsZoom ? Abbreviate(province.Name) : province.Name;
            var labelWidth = _zoom < DetailedLabelsZoom ? 104f : 150f;
            var isSelected = string.Equals(_selectedId, province.Id, StringComparison.Ordinal);
            var labelColor = isSelected ? new Color("ffe49a") : new Color(0.94f, 0.94f, 0.88f, 0.94f);
            DrawString(
                ThemeDB.FallbackFont,
                center + new Vector2(-labelWidth / 2f, 5f),
                label,
                HorizontalAlignment.Center,
                labelWidth,
                14,
                labelColor);

            if (_zoom >= DetailedLabelsZoom && province.SettlementName is { Length: > 0 } settlement)
            {
                var marker = center + new Vector2(0f, 15f);
                DrawCircle(marker, 3.2f, new Color("e3c77f"));
                DrawString(
                    ThemeDB.FallbackFont,
                    marker + new Vector2(-70f, 16f),
                    settlement,
                    HorizontalAlignment.Center,
                    140f,
                    11,
                    new Color(0.87f, 0.82f, 0.68f, 0.88f));
            }

            if (_zoom >= ArmyDetailZoom && _armyStrengthByProvinceId.TryGetValue(province.Id, out var strength))
            {
                var marker = center + new Vector2(22f, -19f);
                DrawCircle(marker, 9f, new Color(0.1f, 0.12f, 0.11f, 0.92f));
                DrawCircle(marker, 7f, new Color("b89654"));
                DrawString(
                    ThemeDB.FallbackFont,
                    marker + new Vector2(-12f, 4f),
                    strength.ToString(),
                    HorizontalAlignment.Center,
                    24f,
                    10,
                    Colors.White);
            }
        }

        if (_zoom < DetailedLabelsZoom) return;

        foreach (var (name, river) in Rivers)
        {
            var midpoint = river[river.Length / 2];
            var position = MapToScreen(Project(midpoint.Longitude, midpoint.Latitude));
            if (!new Rect2(Vector2.Zero, Size).HasPoint(position)) continue;

            DrawString(
                ThemeDB.FallbackFont,
                position,
                name,
                HorizontalAlignment.Left,
                -1f,
                10,
                new Color(0.52f, 0.72f, 0.76f, 0.78f));
        }
    }

    private void DrawMapChrome()
    {
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(14f, 24f),
            "WESTERN EUROPE  -  480 AD",
            HorizontalAlignment.Left,
            -1f,
            13,
            new Color("c9bb96"));

        if (_zoom <= 1.3f)
        {
            DrawContextLabel("BRITANNIA", ProjectToScreen(-4.5d, 55d));
            DrawContextLabel("HISPANIA", ProjectToScreen(-4.5d, 39.5d));
            DrawContextLabel("GERMANIA", ProjectToScreen(10d, 51d));
            DrawContextLabel("ITALIA", ProjectToScreen(12d, 42d));
            DrawContextLabel("AFRICA", ProjectToScreen(2d, 36.2d));
            DrawContextLabel("ATLANTIC", ProjectToScreen(-11.5d, 47d));
            DrawContextLabel("MARE INTERNUM", ProjectToScreen(7d, 37.4d));
        }

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(Mathf.Max(12f, Size.X - 229f), Size.Y - 10f),
            $"Natural Earth coastline  |  Zoom {_zoom:0.0}x",
            HorizontalAlignment.Left,
            -1f,
            9,
            new Color(0.7f, 0.72f, 0.64f, 0.62f));
    }

    private void DrawContextLabel(string text, Vector2 position)
    {
        if (!new Rect2(Vector2.Zero, Size).HasPoint(position)) return;
        DrawString(ThemeDB.FallbackFont, position, text, HorizontalAlignment.Left, -1f, 11, ContextLabelColor);
    }

    private void SelectProvince(Vector2 mapPosition)
    {
        foreach (var province in _provinces.Reverse())
        {
            if (!_provincePolygons.TryGetValue(province.Id, out var polygons)
                || !polygons.Any(polygon => ContainsPoint(polygon, mapPosition)))
            {
                continue;
            }

            _selectedId = province.Id;
            ProvinceSelected?.Invoke(province);
            QueueRedraw();
            AcceptEvent();
            return;
        }
    }

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

    private static Texture2D? LoadTexture(string resourcePath)
    {
        if (ResourceLoader.Exists(resourcePath))
        {
            return GD.Load<Texture2D>(resourcePath);
        }

        var image = new Image();
        var error = image.LoadPngFromBuffer(Godot.FileAccess.GetFileAsBytes(resourcePath));
        if (error == Error.Ok)
        {
            return ImageTexture.CreateFromImage(image);
        }

        GD.PushWarning($"Could not load map texture '{resourcePath}': {error}.");
        return null;
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
        if (!overlapsViewport) return;

        var polygon = points.ToArray();
        _contextLandPolygons.Add(polygon);
        if (string.Equals(sourceCode, "FRA", StringComparison.Ordinal))
        {
            _gaulLandCandidates.Add(polygon);
        }
    }

    private void BuildProvincePolygons()
    {
        _provincePolygons.Clear();
        if (Size.X <= 0f || Size.Y <= 0f) return;

        if (_gaulLandCandidates.Count == 0)
        {
            foreach (var province in _provinces)
            {
                _provincePolygons[province.Id] = [ToVectors(province)];
            }

            return;
        }

        // The reduced source also contains French islands. The largest European
        // polygon is mainland Gaul; internal provinces are clipped to its coast.
        var gaulLand = _gaulLandCandidates.MaxBy(PolygonArea)!.Select(ToMapPoint).ToArray();

        foreach (var province in _provinces)
        {
            var clipped = Geometry2D.IntersectPolygons(ToVectors(province), gaulLand)
                .Where(polygon => polygon.Length >= 3 && !Geometry2D.IsPolygonClockwise(polygon))
                .ToArray();
            _provincePolygons[province.Id] = clipped.Length > 0 ? clipped : [ToVectors(province)];
        }
    }

    private void PanBy(Vector2 amount)
    {
        _pan += amount;
        ClampPan();
        QueueRedraw();
    }

    private void ZoomAt(Vector2 screenPosition, float factor)
    {
        var mapPosition = ScreenToMap(screenPosition);
        var previousZoom = _zoom;
        _zoom = Mathf.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        if (Mathf.IsEqualApprox(previousZoom, _zoom)) return;

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

    private static Vector2 Centroid(IReadOnlyList<Vector2> polygon) =>
        polygon.Aggregate(Vector2.Zero, (sum, point) => sum + point) / polygon.Count;

    private static string Abbreviate(string name) => name.Length <= 10 ? name : $"{name[..7]}.";

    private static Vector2 Normalize(double longitude, double latitude) => new(
        (float)((longitude - MinimumLongitude) / (MaximumLongitude - MinimumLongitude)),
        (float)((MaximumLatitude - latitude) / (MaximumLatitude - MinimumLatitude)));

    private Vector2 ToMapPoint(Vector2 normalized) => new(normalized.X * Size.X, normalized.Y * Size.Y);

    private Vector2 Project(double longitude, double latitude) => ToMapPoint(Normalize(longitude, latitude));

    private Vector2 ProjectToScreen(double longitude, double latitude) => MapToScreen(Project(longitude, latitude));

    private Vector2 MapToScreen(Vector2 mapPosition) => (mapPosition * _zoom) + _pan;

    private Vector2 ScreenToMap(Vector2 screenPosition) => (screenPosition - _pan) / _zoom;

    private Vector2[] ToVectors(ProvinceDefinition province) =>
        province.MapPolygon
            .Select(point => new Vector2(point.X * Size.X / DesignWidth, point.Y * Size.Y / DesignHeight))
            .ToArray();

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

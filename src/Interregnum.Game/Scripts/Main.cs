using System.Text;
using Godot;
using Interregnum.Core.Domain;
using Interregnum.Core.Scenarios;

namespace Interregnum.Game;

public sealed partial class Main : Control
{
    private readonly Label _provinceTitle = new();
    private readonly Label _provinceDetails = new();
    private WorldState? _world;

    public override void _Ready()
    {
        try
        {
            var definition = LoadScenario("res://Data/gaul-480.json");
            _world = ScenarioLoader.CreateWorld(definition);
            BuildInterface(definition);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            AddChild(new Label { Text = $"Could not load scenario:\n{exception.Message}" });
        }
    }

    private void BuildInterface(ScenarioDefinition definition)
    {
        var background = new ColorRect
        {
            Color = new Color("11181b"),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var title = new Label
        {
            Text = $"INTERREGNUM   •   {definition.Name}",
            Position = new Vector2(28, 18)
        };
        StyleLabel(title, 22, new Color("e7d7af"));
        AddChild(title);

        var playableFaction = _world!.Factions.Values.Single(faction => faction.IsPlayable);
        var status = new Label
        {
            Text = $"{playableFaction.Name}    Treasury  {playableFaction.Treasury} solidi    " +
                   $"Food  {playableFaction.FoodReserves}    Turn {_world.Turn}",
            Position = new Vector2(28, 53)
        };
        StyleLabel(status, 16);
        AddChild(status);

        var mapFrame = new Panel
        {
            Position = new Vector2(24, 88),
            Size = new Vector2(1056, 788)
        };
        AddChild(mapFrame);

        var map = new ProvinceMap
        {
            Position = new Vector2(18, 12),
            Size = new Vector2(1020, 760),
            MouseFilter = MouseFilterEnum.Stop,
            ClipContents = true
        };
        map.LoadScenario(definition);
        map.ProvinceSelected += ShowProvince;
        mapFrame.AddChild(map);

        var panel = new Panel
        {
            Position = new Vector2(1100, 88),
            Size = new Vector2(316, 788)
        };
        AddChild(panel);

        _provinceTitle.Text = "Select a province";
        _provinceTitle.Position = new Vector2(22, 22);
        StyleLabel(_provinceTitle, 21, new Color("e7d7af"));
        panel.AddChild(_provinceTitle);

        _provinceDetails.Text = "Click the map to inspect its people,\neconomy, government, and neighbors.";
        _provinceDetails.Position = new Vector2(22, 68);
        StyleLabel(_provinceDetails, 16);
        panel.AddChild(_provinceDetails);

        var zoomOut = new Button { Text = "−", Position = new Vector2(22, 610), Size = new Vector2(54, 42) };
        zoomOut.Pressed += map.ZoomOut;
        panel.AddChild(zoomOut);

        var resetView = new Button { Text = "Reset view", Position = new Vector2(84, 610), Size = new Vector2(126, 42) };
        resetView.Pressed += map.ResetView;
        panel.AddChild(resetView);

        var zoomIn = new Button { Text = "+", Position = new Vector2(218, 610), Size = new Vector2(54, 42) };
        zoomIn.Pressed += map.ZoomIn;
        panel.AddChild(zoomIn);

        var mapHelp = new Label
        {
            Text = "Mouse wheel to zoom\nMiddle-drag to pan",
            Position = new Vector2(22, 664)
        };
        StyleLabel(mapHelp, 13, new Color("92a0a3"));
        panel.AddChild(mapHelp);

        var milestone = new Label
        {
            Text = "MILESTONE 0\n\nThe map and validated scenario are live.\nTurn actions arrive in Milestone 1.",
            Position = new Vector2(22, 718)
        };
        StyleLabel(milestone, 14, new Color("92a0a3"));
        panel.AddChild(milestone);
    }

    private void ShowProvince(ProvinceDefinition definition)
    {
        var province = _world!.Provinces[new ProvinceId(definition.Id)];
        var faction = _world.Factions[province.OwnerId];

        _provinceTitle.Text = province.Name;
        _provinceDetails.Text =
            $"Controlled by\n{faction.Name}\n\n" +
            $"Population                 {province.Population:N0}\n" +
            $"Agriculture                {province.AgriculturalProduction}\n" +
            $"Base tax revenue           {province.TaxRevenue}\n" +
            $"Loyalty                    {province.Loyalty}%\n" +
            $"Administrative burden      {province.AdministrativeBurden}\n" +
            $"Taxation                   {province.TaxRate}\n\n" +
            $"Settlement\n{province.Settlement?.Name ?? "None"}\n\n" +
            $"Neighbors\n{string.Join(", ", province.Neighbors.Select(id => _world.Provinces[id].Name))}";
    }

    private static ScenarioDefinition LoadScenario(string resourcePath)
    {
        var json = Godot.FileAccess.GetFileAsString(resourcePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Scenario file '{resourcePath}' could not be read.");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ScenarioLoader.LoadDefinition(stream);
    }

    private static void StyleLabel(Label label, int fontSize, Color? color = null)
    {
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (color is { } value)
        {
            label.AddThemeColorOverride("font_color", value);
        }
    }
}

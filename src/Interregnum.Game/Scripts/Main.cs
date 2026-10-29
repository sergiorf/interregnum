using System.Text;
using Godot;
using Interregnum.Core.Domain;
using Interregnum.Core.Scenarios;
using Interregnum.Core.Simulation;

namespace Interregnum.Game;

public sealed partial class Main : Control
{
	private readonly Label _provinceTitle = new();
	private readonly Label _provinceDetails = new();
	private readonly Label _actionFeedback = new();
	private readonly Label _realmStatus = new();
	private readonly Dictionary<TaxRate, Button> _taxButtons = [];
	private GameSession? _session;
	private ProvinceMap? _map;
	private FactionId _playerId;
	private ProvinceId? _selectedProvinceId;

	private WorldState World => _session!.State;

	public override void _Ready()
	{
		try
		{
			var definition = LoadScenario("res://Data/gaul-480.json");
			var initialState = ScenarioLoader.CreateWorld(definition);
			_playerId = initialState.Factions.Values.Single(faction => faction.IsPlayable).Id;
			_session = new GameSession(initialState);
			_session.StateChanged += ApplyState;
			BuildInterface(definition);
			ApplyState(initialState);
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

		var page = new MarginContainer();
		page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		page.AddThemeConstantOverride("margin_left", 24);
		page.AddThemeConstantOverride("margin_top", 16);
		page.AddThemeConstantOverride("margin_right", 24);
		page.AddThemeConstantOverride("margin_bottom", 24);
		AddChild(page);

		var pageLayout = new VBoxContainer();
		pageLayout.AddThemeConstantOverride("separation", 7);
		page.AddChild(pageLayout);

		var title = new Label { Text = $"INTERREGNUM   -   {definition.Name}" };
		StyleLabel(title, 22, new Color("e7d7af"));
		pageLayout.AddChild(title);

		_realmStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		StyleLabel(_realmStatus, 16);
		pageLayout.AddChild(_realmStatus);

		var workspace = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		workspace.AddThemeConstantOverride("separation", 18);
		pageLayout.AddChild(workspace);

		var mapFrame = new PanelContainer
		{
			CustomMinimumSize = new Vector2(700, 560),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		workspace.AddChild(mapFrame);

		var mapMargin = new MarginContainer();
		mapMargin.AddThemeConstantOverride("margin_left", 10);
		mapMargin.AddThemeConstantOverride("margin_top", 10);
		mapMargin.AddThemeConstantOverride("margin_right", 10);
		mapMargin.AddThemeConstantOverride("margin_bottom", 10);
		mapFrame.AddChild(mapMargin);

		var map = new ProvinceMap
		{
			CustomMinimumSize = new Vector2(680, 540),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
			FocusMode = FocusModeEnum.All,
			ClipContents = true,
			TooltipText = "Select a province. Wheel or +/- zooms; middle-drag or arrows pan; 0 resets."
		};
		map.LoadScenario(definition);
		map.ProvinceSelected += ShowProvince;
		mapMargin.AddChild(map);
		_map = map;

		var sidePanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(316, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		workspace.AddChild(sidePanel);

		var sideMargin = new MarginContainer();
		sideMargin.AddThemeConstantOverride("margin_left", 18);
		sideMargin.AddThemeConstantOverride("margin_top", 16);
		sideMargin.AddThemeConstantOverride("margin_right", 18);
		sideMargin.AddThemeConstantOverride("margin_bottom", 16);
		sidePanel.AddChild(sideMargin);

		var sideLayout = new VBoxContainer();
		sideLayout.AddThemeConstantOverride("separation", 10);
		sideMargin.AddChild(sideLayout);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		sideLayout.AddChild(scroll);

		var detailsLayout = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(268, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		detailsLayout.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(detailsLayout);

		_provinceTitle.Text = "Select a province";
		StyleLabel(_provinceTitle, 21, new Color("e7d7af"));
		detailsLayout.AddChild(_provinceTitle);

		_provinceDetails.Text = "Click the map to inspect its people, economy, government, and neighbors.";
		_provinceDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		StyleLabel(_provinceDetails, 15);
		detailsLayout.AddChild(_provinceDetails);

		detailsLayout.AddChild(new HSeparator());
		var taxHeading = new Label { Text = "Tax policy" };
		StyleLabel(taxHeading, 16, new Color("e7d7af"));
		detailsLayout.AddChild(taxHeading);

		var taxButtons = new HBoxContainer();
		taxButtons.AddThemeConstantOverride("separation", 7);
		detailsLayout.AddChild(taxButtons);
		AddTaxButton(taxButtons, TaxRate.Low);
		AddTaxButton(taxButtons, TaxRate.Normal);
		AddTaxButton(taxButtons, TaxRate.High);

		_actionFeedback.Text = "Select one of your provinces to set its tax policy.";
		_actionFeedback.CustomMinimumSize = new Vector2(0, 74);
		_actionFeedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		StyleLabel(_actionFeedback, 13, new Color("92a0a3"));
		detailsLayout.AddChild(_actionFeedback);

		detailsLayout.AddChild(new HSeparator());
		var mapHeading = new Label { Text = "Map view" };
		StyleLabel(mapHeading, 16, new Color("e7d7af"));
		detailsLayout.AddChild(mapHeading);

		var zoomButtons = new HBoxContainer();
		zoomButtons.AddThemeConstantOverride("separation", 7);
		detailsLayout.AddChild(zoomButtons);
		AddViewButton(zoomButtons, "-", "Zoom out", map.ZoomOut);
		AddViewButton(zoomButtons, "Reset", "Reset map view", map.ResetView);
		AddViewButton(zoomButtons, "+", "Zoom in", map.ZoomIn);

		var mapHelp = new Label
		{
			Text = "Wheel or +/-: zoom\nMiddle-drag or arrows/WASD: pan\n0 or Home: reset view",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		StyleLabel(mapHelp, 13, new Color("92a0a3"));
		detailsLayout.AddChild(mapHelp);

		var endTurn = new Button
		{
			Text = "End Turn",
			CustomMinimumSize = new Vector2(0, 44),
			TooltipText = "Resolve the deterministic economy, loyalty, rebellion, and stability pipeline."
		};
		endTurn.Pressed += EndTurn;
		sideLayout.AddChild(endTurn);
	}

	private void AddTaxButton(Control parent, TaxRate taxRate)
	{
		var button = new Button
		{
			Text = taxRate.ToString(),
			CustomMinimumSize = new Vector2(0, 42),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Disabled = true,
			TooltipText = $"Set {taxRate.ToString().ToLowerInvariant()} taxation for the selected province."
		};
		button.Pressed += () => SetTaxRate(taxRate);
		_taxButtons.Add(taxRate, button);
		parent.AddChild(button);
	}

	private static void AddViewButton(Control parent, string text, string tooltip, Action action)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			TooltipText = tooltip
		};
		button.Pressed += action;
		parent.AddChild(button);
	}

	private void ApplyState(WorldState state)
	{
		_map?.ApplyWorldState(state);
		RefreshRealmStatus();
		RefreshSelectedProvince();
	}

	private void ShowProvince(ProvinceDefinition definition)
	{
		_selectedProvinceId = new ProvinceId(definition.Id);
		RefreshSelectedProvince();

		var province = World.Provinces[_selectedProvinceId.Value];
		_actionFeedback.Text = province.OwnerId == _playerId && province.IsRebelling
			? "This province is rebelling. Low tax can restore loyalty; tax resumes the turn after recovery."
			: province.OwnerId == _playerId
				? "Choose Low, Normal, or High taxation for this province."
				: "Only provinces you control can receive tax orders.";
		_actionFeedback.AddThemeColorOverride("font_color", new Color("92a0a3"));
	}

	private void RefreshSelectedProvince()
	{
		if (_selectedProvinceId is not { } provinceId) return;

		var province = World.Provinces[provinceId];
		var faction = World.Factions[province.OwnerId];

		_provinceTitle.Text = province.Name;
		_provinceDetails.Text =
			$"Controlled by\n{faction.Name}\n\n" +
			$"Population                 {province.Population:N0}\n" +
			$"Agriculture                {province.AgriculturalProduction}\n" +
			$"Base tax revenue           {province.TaxRevenue}\n" +
			$"Loyalty                    {province.Loyalty}%\n" +
			$"Condition                  {ProvinceCondition(province)}\n" +
			$"Administrative burden      {province.AdministrativeBurden}\n" +
			$"Taxation                   {province.TaxRate}\n\n" +
			$"Settlement\n{province.Settlement?.Name ?? "None"}\n\n" +
			$"Neighbors\n{string.Join(", ", province.Neighbors.Select(id => World.Provinces[id].Name))}";

		foreach (var button in _taxButtons.Values)
		{
			button.Disabled = province.OwnerId != _playerId;
		}
	}

	private void SetTaxRate(TaxRate taxRate)
	{
		if (_selectedProvinceId is not { } provinceId) return;

		var result = _session!.Execute(new SetProvinceTaxRate(_playerId, provinceId, taxRate));
		_actionFeedback.Text = result.Succeeded
			? $"Tax policy changed to {taxRate}. Revenue is collected when a turn ends."
			: result.ErrorMessage ?? $"Command rejected: {result.ErrorCode}.";
		_actionFeedback.AddThemeColorOverride(
			"font_color",
			result.Succeeded ? new Color("9fc59f") : new Color("d6a28f"));
	}

	private void EndTurn()
	{
		var result = _session!.Execute(new EndTurn(_playerId));
		if (result.Succeeded)
		{
			var report = result.TurnReport!.Factions.Single(item => item.FactionId == _playerId);
			_actionFeedback.Text =
				$"Turn {result.TurnReport.CompletedTurn}: Food {report.OpeningFood} +{report.FoodProduction} " +
				$"-{report.FoodConsumed} = {report.ClosingFood}. Treasury {report.OpeningTreasury} " +
				$"+{report.TaxRevenue} -{report.UpkeepPaid} -{report.AdministrationPaid} = {report.ClosingTreasury}. " +
				$"Stability {report.Stability.OpeningStability} {FormatSigned(report.Stability.Change)} " +
				$"= {report.Stability.ClosingStability}.";
			if (report.UnmetFood > 0 || report.UpkeepUnpaid > 0 || report.AdministrationUnpaid > 0)
			{
				_actionFeedback.Text +=
					$" Unmet: food {report.UnmetFood}, upkeep {report.UpkeepUnpaid}, administration {report.AdministrationUnpaid}.";
			}

			var transitions = report.RebellionByProvince.Where(item => item.Changed).ToArray();
			if (transitions.Length > 0)
			{
				_actionFeedback.Text += " " + string.Join(
					" ",
					transitions.Select(item =>
						$"{World.Provinces[item.ProvinceId].Name} " +
						(item.ClosingIsRebelling ? "rebelled." : "recovered.")));
			}

			_actionFeedback.TooltipText = BuildTurnTooltip(report);
			_actionFeedback.AddThemeColorOverride("font_color", new Color("9fc59f"));
		}
		else
		{
			_actionFeedback.Text = result.ErrorMessage ?? $"Command rejected: {result.ErrorCode}.";
			_actionFeedback.AddThemeColorOverride("font_color", new Color("d6a28f"));
		}
	}

	private void RefreshRealmStatus()
	{
		if (_session is null) return;

		var player = World.Factions[_playerId];
		var rebellions = World.Provinces.Values.Count(
			province => province.OwnerId == _playerId && province.IsRebelling);
		var atRisk = World.Provinces.Values.Count(
			province => province.OwnerId == _playerId && province.IsAtRisk);
		_realmStatus.Text =
			$"{player.Name}    Treasury {player.Treasury} solidi    Food {player.FoodReserves}    " +
			$"Stability {player.Stability}    At risk {atRisk}    Rebellions {rebellions}    Turn {World.Turn}";
	}

	private string BuildTurnTooltip(FactionTurnReport report)
	{
		var loyalty = string.Join(
			"\n",
			report.LoyaltyByProvince.Select(item =>
				$"{World.Provinces[item.ProvinceId].Name}: {item.OpeningLoyalty} " +
				$"{FormatSigned(item.TaxPolicyModifier)} tax " +
				$"{FormatSigned(item.FoodShortageModifier)} food " +
				$"{FormatSigned(item.AdministrationModifier)} administration " +
				$"{FormatSigned(item.ClampAdjustment)} clamp = {item.ClosingLoyalty}"));
		return
			$"Food: {report.OpeningFood} + {report.FoodProduction} - {report.FoodConsumed} = {report.ClosingFood}\n" +
			$"Treasury: {report.OpeningTreasury} + {report.TaxRevenue} - {report.UpkeepPaid} - {report.AdministrationPaid} = {report.ClosingTreasury}\n" +
			$"Stability: {report.Stability.OpeningStability} {FormatSigned(report.Stability.RecoveryModifier)} recovery " +
			$"{FormatSigned(report.Stability.RebellionModifier)} rebellion " +
			$"{FormatSigned(report.Stability.FoodShortageModifier)} food " +
			$"{FormatSigned(report.Stability.UpkeepModifier)} upkeep " +
			$"{FormatSigned(report.Stability.AdministrationModifier)} administration " +
			$"{FormatSigned(report.Stability.ClampAdjustment)} clamp = {report.Stability.ClosingStability}\n\n" +
			loyalty;
	}

	private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

	private static string ProvinceCondition(Province province) => province.IsRebelling
		? "REBELLION"
		: province.IsAtRisk ? "At risk" : "Governed";

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

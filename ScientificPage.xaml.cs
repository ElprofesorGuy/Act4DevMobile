namespace CalculatriceMaui;

public partial class ScientificPage : ContentPage
{
    private readonly CalculatorController _controller;
    private bool _isLandscape;
    private bool _layoutApplied;

    public ScientificPage()
    {
        InitializeComponent();
        _controller = new CalculatorController(RefreshDisplay);
        ExpressionScroll.SizeChanged += (_, _) => ExpressionLabel.MinimumWidthRequest = ExpressionScroll.Width;
        DisplayCard.SizeChanged += (_, _) => RefreshDisplay();
        RefreshDisplay();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        bool landscape = width > height;
        if (_layoutApplied && landscape == _isLandscape) return;

        _isLandscape = landscape;
        _layoutApplied = true;
        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        if (landscape)
        {
            RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.5, GridUnitType.Star)));
            Grid.SetRow(DisplayCard, 0);
            Grid.SetColumn(DisplayCard, 0);
            Grid.SetRow(KeypadScroll, 0);
            Grid.SetColumn(KeypadScroll, 1);
        }
        else
        {
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(3, GridUnitType.Star)));
            Grid.SetRow(DisplayCard, 0);
            Grid.SetColumn(DisplayCard, 0);
            Grid.SetRow(KeypadScroll, 1);
            Grid.SetColumn(KeypadScroll, 0);
        }

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (ResultLabel is null) return;

        ResultLabel.Text = _controller.Engine.Display;
        ExpressionLabel.Text = string.IsNullOrEmpty(_controller.Engine.Expression) ? "\u00A0" : _controller.Engine.Expression;
        MemoryLabel.IsVisible = _controller.Engine.HasMemory;
        AngleLabel.Text = _controller.Engine.UseDegrees ? "DEG" : "RAD";
        AngleButton.Text = AngleLabel.Text;
        SinButton.Text = _controller.SecondMode ? "asin" : "sin";
        CosButton.Text = _controller.SecondMode ? "acos" : "cos";
        TanButton.Text = _controller.SecondMode ? "atan" : "tan";
        SetActive(SecondButton, _controller.SecondMode);
        SetActive(FractionButton, _controller.Engine.OutputFormat == Models.ResultFormat.Fraction);
        SetActive(SciButton, _controller.Engine.OutputFormat == Models.ResultFormat.Scientific);

        bool error = _controller.Engine.HasError;
        ResultLabel.LineBreakMode = error ? LineBreakMode.WordWrap : LineBreakMode.NoWrap;
        ResultLabel.MaxLines = error ? 2 : 1;
        if (error)
        {
            ResultLabel.FontSize = _isLandscape ? 18 : 24;
            ResultLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B42318"), Color.FromArgb("#FF6B6B"));
        }
        else
        {
            double baseSize = _isLandscape ? 42 : 56;
            double available = DisplayCard.Width - 36;
            double fitted = available > 0 ? available / (Math.Max(1, ResultLabel.Text.Length) * 0.58) : baseSize;
            ResultLabel.FontSize = Math.Clamp(fitted, 14, baseSize);
            ResultLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1B1D22"), Colors.White);
        }

        Dispatcher?.Dispatch(async () =>
            await ExpressionScroll.ScrollToAsync(ExpressionScroll.ContentSize.Width, 0, false));
    }

    private void SetActive(Button button, bool active) =>
        button.Style = (Style)Resources[active ? "FunctionKeyActive" : "FunctionKey"];

    private void OnDigitClicked(object? sender, EventArgs e) => _controller.OnDigitClicked(sender, e);
    private void OnOperatorClicked(object? sender, EventArgs e) => _controller.OnOperatorClicked(sender, e);
    private void OnUnaryClicked(object? sender, EventArgs e) => _controller.OnUnaryClicked(sender, e);
    private void OnConstantClicked(object? sender, EventArgs e) => _controller.OnConstantClicked(sender, e);
    private void OnMemoryClicked(object? sender, EventArgs e) => _controller.OnMemoryClicked(sender, e);
    private void OnActionClicked(object? sender, EventArgs e) => _controller.OnActionClicked(sender, e);
    private void OnFractionBarClicked(object? sender, EventArgs e) => _controller.OnFractionBarClicked(sender, e);
    private void OnAngleModeClicked(object? sender, EventArgs e) => _controller.OnAngleModeClicked(sender, e);
    private void OnSecondClicked(object? sender, EventArgs e) => _controller.OnSecondClicked(sender, e);
    private void OnResultFormatClicked(object? sender, EventArgs e) => _controller.OnResultFormatClicked(sender, e);
}
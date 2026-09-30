namespace CalculatriceMaui;

public partial class MainPage : ContentPage
{
    private readonly CalculatorController _controller;
    private bool _isLandscape;
    private bool _layoutApplied;

    public MainPage()
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
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.35, GridUnitType.Star)));
            Grid.SetRow(DisplayCard, 0);
            Grid.SetColumn(DisplayCard, 0);
            Grid.SetRow(KeypadPanel, 0);
            Grid.SetColumn(KeypadPanel, 1);
        }
        else
        {
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
            RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(2.2, GridUnitType.Star)));
            Grid.SetRow(DisplayCard, 0);
            Grid.SetColumn(DisplayCard, 0);
            Grid.SetRow(KeypadPanel, 1);
            Grid.SetColumn(KeypadPanel, 0);
        }

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (ResultLabel is null) return;

        ResultLabel.Text = _controller.Engine.Display;
        ExpressionLabel.Text = string.IsNullOrEmpty(_controller.Engine.Expression) ? "\u00A0" : _controller.Engine.Expression;

        bool error = _controller.Engine.HasError;
        ResultLabel.LineBreakMode = error ? LineBreakMode.WordWrap : LineBreakMode.NoWrap;
        ResultLabel.MaxLines = error ? 2 : 1;
        if (error)
        {
            ResultLabel.FontSize = _isLandscape ? 20 : 26;
            ResultLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B42318"), Color.FromArgb("#FF6B6B"));
        }
        else
        {
            double baseSize = _isLandscape ? 46 : 64;
            double available = DisplayCard.Width - 40;
            double fitted = available > 0 ? available / (Math.Max(1, ResultLabel.Text.Length) * 0.58) : baseSize;
            ResultLabel.FontSize = Math.Clamp(fitted, 14, baseSize);
            ResultLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1B1D22"), Colors.White);
        }

        Dispatcher?.Dispatch(async () =>
            await ExpressionScroll.ScrollToAsync(ExpressionScroll.ContentSize.Width, 0, false));
    }

    private void OnDigitClicked(object? sender, EventArgs e) => _controller.OnDigitClicked(sender, e);
    private void OnOperatorClicked(object? sender, EventArgs e) => _controller.OnOperatorClicked(sender, e);
    private void OnActionClicked(object? sender, EventArgs e) => _controller.OnActionClicked(sender, e);
}
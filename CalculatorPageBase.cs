using CalculatriceMaui.Models;

namespace CalculatriceMaui;

public sealed class CalculatorController
{
    public CalculatorEngine Engine { get; } = new();
    private readonly Action _refreshDisplay;
    public bool SecondMode { get; private set; }

    public CalculatorController(Action refreshDisplay) => _refreshDisplay = refreshDisplay;

    private void RefreshDisplay() => _refreshDisplay();

    public void OnDigitClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Text, out int digit))
        {
            Engine.InputDigit(digit);
            RefreshDisplay();
        }
    }

    public void OnOperatorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string parameter } && parameter.Length > 0)
        {
            Engine.SetOperator(parameter[0]);
            RefreshDisplay();
        }
    }

    public void OnUnaryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string parameter }
            || !Enum.TryParse(parameter, out UnaryOperation operation)) return;

        if (SecondMode)
        {
            operation = operation switch
            {
                UnaryOperation.Sin => UnaryOperation.Asin,
                UnaryOperation.Cos => UnaryOperation.Acos,
                UnaryOperation.Tan => UnaryOperation.Atan,
                _ => operation
            };
        }

        Engine.ApplyUnary(operation);
        RefreshDisplay();
    }

    public void OnConstantClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string parameter }
            && Enum.TryParse(parameter, out MathConstant constant))
        {
            Engine.InsertConstant(constant);
            RefreshDisplay();
        }
    }

    public void OnMemoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string action }) return;

        switch (action)
        {
            case "MC": Engine.MemoryClear(); break;
            case "MR": Engine.MemoryRecall(); break;
            case "MPLUS": Engine.MemoryAdd(); break;
            case "MMINUS": Engine.MemorySubtract(); break;
            case "MS": Engine.MemoryStore(); break;
        }

        RefreshDisplay();
    }

    public void OnActionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string action }) return;

        switch (action)
        {
            case "CLEAR": Engine.Clear(); break;
            case "BACK": Engine.Backspace(); break;
            case "SIGN": Engine.ToggleSign(); break;
            case "PERCENT": Engine.Percent(); break;
            case "DOT": Engine.InputDecimal(); break;
            case "EQUALS": Engine.Evaluate(); break;
        }

        RefreshDisplay();
    }

    public void OnFractionBarClicked(object? sender, EventArgs e)
    {
        Engine.InputFractionBar();
        RefreshDisplay();
    }

    public void OnAngleModeClicked(object? sender, EventArgs e)
    {
        Engine.ToggleAngleUnit();
        RefreshDisplay();
    }

    public void OnSecondClicked(object? sender, EventArgs e)
    {
        SecondMode = !SecondMode;
        RefreshDisplay();
    }

    public void OnResultFormatClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string parameter }
            && Enum.TryParse(parameter, out ResultFormat requested))
        {
            Engine.SetOutputFormat(Engine.OutputFormat == requested ? ResultFormat.Decimal : requested);
            RefreshDisplay();
        }
    }
}
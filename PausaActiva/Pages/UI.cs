using Microsoft.Maui.Controls.Shapes;

namespace PausaActiva.Pages;

public static class UI
{
    public static readonly Color Background = Color.FromArgb("#101820");
    public static readonly Color Surface = Color.FromArgb("#192630");
    public static readonly Color Raised = Color.FromArgb("#233541");
    public static readonly Color Text = Color.FromArgb("#EDF4F7");
    public static readonly Color Muted = Color.FromArgb("#9BAEBB");
    public static readonly Color Mint = Color.FromArgb("#78E7BB");
    public static readonly Color Blue = Color.FromArgb("#81C7FF");
    public static readonly Color Amber = Color.FromArgb("#FFD27D");
    public static readonly Color Red = Color.FromArgb("#FFA6AA");
    public static Label Label(string text, double size = 16, Color? color = null, bool bold = false) => new()
    { Text = text, FontSize = size, TextColor = color ?? Text, FontFamily = bold ? "OpenSansSemibold" : "OpenSansRegular", LineBreakMode = LineBreakMode.WordWrap };
    public static Button Button(string text, Action action, bool primary = false, bool danger = false)
    {
        var button = new Button { Text = text, BackgroundColor = primary ? Mint : Raised, TextColor = primary ? Background : danger ? Red : Text, Padding = new Thickness(18, 10) };
        button.Clicked += (_, _) => action();
        return button;
    }
    public static Border Card(View content, Color? stroke = null, double padding = 22) => new()
    { Content = content, Padding = padding, BackgroundColor = Surface, Stroke = stroke ?? Raised, StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 20 } };
    public static VerticalStackLayout Stack(params View[] views)
    {
        var stack = new VerticalStackLayout { Spacing = 14 };
        foreach (var view in views) stack.Add(view);
        return stack;
    }
    public static HorizontalStackLayout Row(params View[] views)
    {
        var row = new HorizontalStackLayout { Spacing = 10 };
        foreach (var view in views) row.Add(view);
        return row;
    }
    public static string Duration(double seconds) => seconds >= 60 ? $"{(int)(seconds / 60)} min {seconds % 60:0.#} s" : $"{seconds:0.#} s";
    public static async Task Error(Page page, Exception ex) => await page.DisplayAlertAsync("No se pudo completar", ex.Message, "Entendido");
}

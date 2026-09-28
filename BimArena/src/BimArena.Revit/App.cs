using System.Reflection;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimArena.Revit;

public sealed class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        // This one-argument overload puts the panel on Revit's EXISTING Add-Ins tab.
        var panel = application.CreateRibbonPanel("BIM Arena");
        var data = new PushButtonData("BimArena.Play", "Play\nBIM Arena", Assembly.GetExecutingAssembly().Location, typeof(PlayCommand).FullName!)
        {
            AvailabilityClassName = typeof(GameAvailability).FullName!,
            ToolTip = "Play a Doom-style survival game inside the active 3D view.",
            LongDescription = "Click a clear floor point to start. Visible model geometry becomes the arena. WASD moves, mouse aims, click fires, and Esc returns to Revit. The model is never modified."
        };
        var button = (PushButton)panel.AddItem(data); button.LargeImage = CreateIcon(); button.Image = button.LargeImage;
        return Result.Succeeded;
    }
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static ImageSource CreateIcon()
    {
        var group = new DrawingGroup();
        using (var d = group.Open())
        {
            d.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 32, 42)), null, new(0, 0, 32, 32), 5, 5);
            var pen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 182, 110)), 2.5);
            d.DrawEllipse(null, pen, new(16, 16), 8, 8);
            d.DrawLine(pen, new(16, 3), new(16, 11)); d.DrawLine(pen, new(16, 21), new(16, 29));
            d.DrawLine(pen, new(3, 16), new(11, 16)); d.DrawLine(pen, new(21, 16), new(29, 16));
        }
        var image = new DrawingImage(group); image.Freeze(); return image;
    }
}

public sealed class GameAvailability : IExternalCommandAvailability
{
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) =>
        applicationData.ActiveUIDocument?.ActiveView is View3D { IsTemplate: false };
}

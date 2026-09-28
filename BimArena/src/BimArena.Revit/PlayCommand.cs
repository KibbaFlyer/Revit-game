using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BimArena.Core;
using BimArena.Desktop;

namespace BimArena.Revit;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public sealed class PlayCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var ui = commandData.Application; var document = ui.ActiveUIDocument;
        if (document?.ActiveView is not View3D { IsTemplate: false } view)
        {
            TaskDialog.Show("BIM Arena", "Open a 3D view, then press Play BIM Arena."); return Result.Cancelled;
        }
        try
        {
            var activeWindow = document.GetOpenUIViews().FirstOrDefault(v => v.ViewId == view.Id);
            if (activeWindow == null) throw new InvalidOperationException("The active 3D view has no visible viewport.");
            var selected = document.Selection.PickObject(ObjectType.PointOnElement,
                "BIM Arena: click an open FLOOR surface to spawn here. Esc cancels.");
            var origin = selected.GlobalPoint;
            if (origin == null) throw new InvalidOperationException("Select a point on a visible floor surface.");
            var context = new ViewGeometryExport(origin);
            using (var exporter = new CustomExporter(document.Document, context) { IncludeGeometricObjects = false, ShouldStopOnError = true })
            {
                try { exporter.Export(view); }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) when (context.AbortReason != null)
                { throw new InvalidOperationException(context.AbortReason); }
            }
            if (context.AbortReason != null) throw new InvalidOperationException(context.AbortReason);
            var orientation = view.GetOrientation();
            var forward = orientation.ForwardDirection;
            var scene = new SceneData(view.Name, context.Triangles.ToArray(), V3.Zero, new(forward.X, forward.Y, 0));
            var session = new GameSession(scene);
            var bounds = activeWindow.GetWindowRectangle();
            var window = new GameWindow(session, ui.MainWindowHandle, new PixelRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            // Modal ownership keeps the captured view/document stable. No API calls occur from rendering/input callbacks.
            window.ShowDialog();
            if (window.Failure is { } failure) throw new InvalidOperationException("The game stopped unexpectedly.", failure);
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
        catch (Exception ex)
        {
            string detail = ex.Message;
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BimArena", "Logs");
                Directory.CreateDirectory(folder); string path = Path.Combine(folder, $"arena-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.WriteAllText(path, ex.ToString()); detail += $"\n\nDetails: {path}";
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            TaskDialog.Show("BIM Arena", detail); return Result.Cancelled;
        }
    }
}

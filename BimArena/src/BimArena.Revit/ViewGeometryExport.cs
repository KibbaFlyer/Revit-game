using System.Diagnostics;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using BimArena.Core;

namespace BimArena.Revit;

/// <summary>Snapshot only what Revit exports from this view, including transformed family/link instances.</summary>
internal sealed class ViewGeometryExport(XYZ origin) : IModelExportContext
{
    public List<Triangle> Triangles { get; } = [];
    public string? AbortReason { get; private set; }
    private readonly Stack<Transform> transforms = new();
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private uint color = 0xFF9CA7A7;
    private const int TriangleBudget = 350_000;
    private const double FeetToMetres = .3048;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public bool Start() { transforms.Push(Transform.Identity); return true; }
    public void Finish() { }
    public bool IsCanceled()
    {
        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) AbortReason = "Arena loading was cancelled.";
        if (elapsed.Elapsed.TotalSeconds > 90) AbortReason = "View export exceeded 90 seconds. Use a section box or hide distant geometry, then try again.";
        return AbortReason != null;
    }
    public RenderNodeAction OnViewBegin(ViewNode node) => RenderNodeAction.Proceed;
    public void OnViewEnd(ElementId elementId) { }
    public RenderNodeAction OnElementBegin(ElementId elementId) => RenderNodeAction.Proceed;
    public void OnElementEnd(ElementId elementId) { }
    public RenderNodeAction OnInstanceBegin(InstanceNode node) { transforms.Push(transforms.Peek().Multiply(node.GetTransform())); return RenderNodeAction.Proceed; }
    public void OnInstanceEnd(InstanceNode node) => transforms.Pop();
    public RenderNodeAction OnLinkBegin(LinkNode node) { transforms.Push(transforms.Peek().Multiply(node.GetTransform())); return RenderNodeAction.Proceed; }
    public void OnLinkEnd(LinkNode node) => transforms.Pop();
    public RenderNodeAction OnFaceBegin(FaceNode node) => RenderNodeAction.Proceed;
    public void OnFaceEnd(FaceNode node) { }
    public void OnMaterial(MaterialNode node)
    {
        var c = node.Color;
        // Quantization reduces draw calls without reducing or approximating collision geometry.
        static byte Quantize(byte v) => (byte)Math.Min(255, (v / 24) * 24 + 12);
        color = c.IsValid ? 0xFF000000u | (uint)Quantize(c.Red) << 16 | (uint)Quantize(c.Green) << 8 | Quantize(c.Blue) : 0xFF9CA7A7;
    }
    public void OnPolymesh(PolymeshTopology node)
    {
        if (AbortReason != null) return;
        if (Triangles.Count + node.NumberOfFacets > TriangleBudget)
        {
            AbortReason = $"This view exceeds the {TriangleBudget:N0}-triangle arena budget. Tighten the section box or hide unnecessary categories and try again. No partial collision map was loaded.";
            return;
        }
        var transform = transforms.Peek();
        var points = node.GetPoints().Select(p =>
        {
            var world = transform.OfPoint(p) - origin;
            return new V3(world.X * FeetToMetres, world.Y * FeetToMetres, world.Z * FeetToMetres);
        }).ToArray();
        foreach (var facet in node.GetFacets())
        {
            var t = new Triangle(points[facet.V1], points[facet.V2], points[facet.V3], color);
            if (V3.Cross(t.B - t.A, t.C - t.A).LengthSquared > 1e-16) Triangles.Add(t);
        }
    }
    // Curves, annotations and lights have no collision volume; tessellated model objects do.
    public RenderNodeAction OnCurve(CurveNode node) => RenderNodeAction.Skip;
    public RenderNodeAction OnPolyline(PolylineNode node) => RenderNodeAction.Skip;
    public RenderNodeAction OnPoint(PointNode node) => RenderNodeAction.Skip;
    public void OnLineSegment(LineSegment segment) { }
    public void OnPolylineSegments(PolylineSegments segments) { }
    public void OnText(TextNode node) { }
    public void OnRPC(RPCNode node) { }
    public void OnLight(LightNode node) { }
}

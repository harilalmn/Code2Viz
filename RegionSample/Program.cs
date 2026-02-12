using C2VGeometry;

// ═══════════════════════════════════════════════════════
// Region A: A "guitar pick" shape using Line + Arc + Bezier
// ═══════════════════════════════════════════════════════

var a0 = VPoint.Internal(0, 0);
var a2 = VPoint.Internal(10, 0);
var a4 = VPoint.Internal(5, 10);

var regionA = new Region(new List<ICurve>
{
    // Bottom: line
    new VLine(a0, a2),

    // Right side: arc bulging outward
    VArc.FromStartEndRadius(a2, a4, 8, false),

    // Top-left: cubic bezier curve
    new VBezier(a4, VPoint.Internal(2, 12), VPoint.Internal(-2, 8), a0)
});

// ═══════════════════════════════════════════════════════
// Region B: An organic "blob" using Spline + Lines + Arc
// Shifted so it partially overlaps Region A
// ═══════════════════════════════════════════════════════

var b0 = VPoint.Internal(4, 3);
var b1 = VPoint.Internal(14, 3);
var b2 = VPoint.Internal(14, 9);
var b3 = VPoint.Internal(4, 9);

var regionB = new Region(new List<ICurve>
{
    // Bottom: spline (wavy bottom edge)
    new VSpline(b0, VPoint.Internal(6, 1), VPoint.Internal(9, 5), VPoint.Internal(12, 2), b1),

    // Right: line
    new VLine(b1, b2),

    // Top: arc (curved top)
    VArc.FromStartEndRadius(b2, b3, 7, false),

    // Left: bezier (organic left edge)
    new VBezier(b3, VPoint.Internal(2, 8), VPoint.Internal(3, 4), b0)
});

// ═══════════════════════════════════════════════════════
// Print info about the regions
// ═══════════════════════════════════════════════════════

Console.WriteLine($"Region A: {regionA}");
Console.WriteLine($"  Area: {regionA.Area:F2}");
Console.WriteLine($"  Perimeter: {regionA.Perimeter:F2}");
Console.WriteLine();

Console.WriteLine($"Region B: {regionB}");
Console.WriteLine($"  Area: {regionB.Area:F2}");
Console.WriteLine($"  Perimeter: {regionB.Perimeter:F2}");
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 1. UNION  (A ∪ B)
// ═══════════════════════════════════════════════════════

var union = RegionBooleanOps.Union(regionA, regionB);
if (union != null)
{
    Console.WriteLine($"UNION: {union}");
    Console.WriteLine($"  Area: {union.Area:F2}");
}
else
{
    Console.WriteLine("UNION: null (disjoint or cannot form single region)");
}
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 2. INTERSECTION  (A ∩ B)
// ═══════════════════════════════════════════════════════

var intersections = RegionBooleanOps.Intersect(regionA, regionB);
Console.WriteLine($"INTERSECTION: {intersections.Count} region(s)");
foreach (var r in intersections)
{
    Console.WriteLine($"  {r}, Area: {r.Area:F2}");
}
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 3. DIFFERENCE  (A - B)
// ═══════════════════════════════════════════════════════

var diffAB = RegionBooleanOps.Difference(regionA, regionB);
Console.WriteLine($"DIFFERENCE (A - B): {diffAB.Count} region(s)");
double diffTotalArea = 0;
foreach (var r in diffAB)
{
    Console.WriteLine($"  {r}, Area: {r.Area:F2}");
    diffTotalArea += r.Area;
}
Console.WriteLine($"  Total Area: {diffTotalArea:F2}");
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 4. XOR  (A ⊕ B) — symmetric difference
// ═══════════════════════════════════════════════════════

var xor = RegionBooleanOps.Xor(regionA, regionB);
Console.WriteLine($"XOR (A xor B): {xor.Count} region(s)");
double xorTotalArea = 0;
foreach (var r in xor)
{
    Console.WriteLine($"  {r}, Area: {r.Area:F2}");
    xorTotalArea += r.Area;
}
Console.WriteLine($"  Total XOR Area: {xorTotalArea:F2}");
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 5. Area Invariant: |A| + |B| - |A ∩ B| == |A ∪ B|
// ═══════════════════════════════════════════════════════

double intersectArea = intersections.Sum(r => r.Area);
double expected = regionA.Area + regionB.Area - intersectArea;
Console.WriteLine("=== Area Invariant Check ===");
Console.WriteLine($"  |A| + |B| - |A int B| = {expected:F2}");
Console.WriteLine($"  |A union B|           = {union?.Area:F2}");
Console.WriteLine($"  Match: {Math.Abs(expected - (union?.Area ?? 0)) < 1.0}");
Console.WriteLine();

// ═══════════════════════════════════════════════════════
// 6. Extension method syntax (fluent API)
// ═══════════════════════════════════════════════════════

Console.WriteLine("=== Extension Methods ===");
var unionExt = regionA.Union(regionB);
Console.WriteLine($"  a.Union(b) area: {unionExt?.Area:F2}");

var intExt = RegionBooleanOps.Intersect(regionA, regionB);
Console.WriteLine($"  a.Intersect(b) count: {intExt.Count}");

var diffExt = regionA.Difference(regionB);
Console.WriteLine($"  a.Difference(b) count: {diffExt.Count}");

var xorExt = regionA.Xor(regionB);
Console.WriteLine($"  a.Xor(b) count: {xorExt.Count}");

// ═══════════════════════════════════════════════════════
// 7. Point containment
// ═══════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("=== Point Containment ===");
var testPoint = VPoint.Internal(5, 5);
Console.WriteLine($"  Point (5,5) in A: {regionA.Contains(testPoint)}");
Console.WriteLine($"  Point (5,5) in B: {regionB.Contains(testPoint)}");
Console.WriteLine($"  Point (5,5) in Union: {union?.Contains(testPoint)}");
Console.WriteLine($"  Point (12,4) in A: {regionA.Contains(VPoint.Internal(12, 4))}");
Console.WriteLine($"  Point (12,4) in B: {regionB.Contains(VPoint.Internal(12, 4))}");

namespace Code2Viz.Editor;

/// <summary>
/// Provides code snippets for common shapes and patterns.
/// Snippets support placeholders: $1, $2, etc. for Tab navigation, $0 for final cursor position.
/// </summary>
public static class CodeSnippets
{
    public static readonly Dictionary<string, (string Description, string Code)> Snippets = new()
    {
        // ===== C# Language Snippets =====

        // Properties
        ["prop"] = ("Auto-implemented property",
@"public $1int $2MyProperty { get; set; }$0"),

        ["propfull"] = ("Property with backing field",
@"private $1int _$2myField;
public $1int $2MyProperty
{
    get { return _$2myField; }
    set { _$2myField = value; }
}$0"),

        ["propg"] = ("Auto property with private setter",
@"public $1int $2MyProperty { get; private set; }$0"),

        ["propi"] = ("Auto property with init setter",
@"public $1int $2MyProperty { get; init; }$0"),

        // Classes and Types
        ["class"] = ("Class definition",
@"public class $1MyClass
{
    public $1MyClass()
    {
        $0
    }
}"),

        ["interface"] = ("Interface definition",
@"public interface I$1MyInterface
{
    $0void MyMethod();
}"),

        ["struct"] = ("Struct definition",
@"public struct $1MyStruct
{
    public $2int Value { get; set; }$0
}"),

        ["record"] = ("Record definition",
@"public record $1MyRecord($2int Id, $3string Name);$0"),

        ["enum"] = ("Enum definition",
@"public enum $1MyEnum
{
    $2None,
    $0
}"),

        // Constructor
        ["ctor"] = ("Constructor",
@"public $1ClassName()
{
    $0
}"),

        // Control Flow
        ["if"] = ("If statement",
@"if ($1condition)
{
    $0
}"),

        ["else"] = ("Else statement",
@"else
{
    $0
}"),

        ["ifel"] = ("If-else statement",
@"if ($1condition)
{
    $2
}
else
{
    $0
}"),

        ["for"] = ("For loop",
@"for (int $1i = 0; $1i < $2length; $1i++)
{
    $0
}"),

        ["forr"] = ("Reverse for loop",
@"for (int $1i = $2length - 1; $1i >= 0; $1i--)
{
    $0
}"),

        ["foreach"] = ("Foreach loop",
@"foreach (var $1item in $2collection)
{
    $0
}"),

        ["while"] = ("While loop",
@"while ($1condition)
{
    $0
}"),

        ["do"] = ("Do-while loop",
@"do
{
    $0
} while ($1condition);"),

        ["switch"] = ("Switch statement",
@"switch ($1variable)
{
    case $2value1:
        $0
        break;
    default:
        break;
}"),

        ["switchex"] = ("Switch expression",
@"var $1result = $2variable switch
{
    $3value1 => $4result1,
    _ => $5defaultResult
};$0"),

        // Exception Handling
        ["try"] = ("Try-catch block",
@"try
{
    $0
}
catch ($1Exception ex)
{
    throw;
}"),

        ["tryf"] = ("Try-finally block",
@"try
{
    $0
}
finally
{
    $1
}"),

        ["trycf"] = ("Try-catch-finally block",
@"try
{
    $0
}
catch ($1Exception ex)
{
    throw;
}
finally
{
    $2
}"),

        ["throw"] = ("Throw exception",
@"throw new $1Exception($2""message"");$0"),

        ["exception"] = ("Exception class",
@"public class $1MyException : Exception
{
    public $1MyException() { }
    public $1MyException(string message) : base(message) { }
    public $1MyException(string message, Exception inner) : base(message, inner) { }
}$0"),

        // Threading and Async
        ["lock"] = ("Lock statement",
@"lock ($1lockObject)
{
    $0
}"),

        ["async"] = ("Async method",
@"public async Task $1MyMethodAsync()
{
    $0await Task.CompletedTask;
}"),

        ["asynct"] = ("Async method with return type",
@"public async Task<$1int> $2MyMethodAsync()
{
    await Task.Delay($3100);
    return $0;
}"),

        // Using and Disposal
        ["using"] = ("Using statement",
@"using (var $1resource = new $2Resource())
{
    $0
}"),

        ["usingd"] = ("Using declaration",
@"using var $1resource = new $2Resource();$0"),

        // Common Patterns
        ["cw"] = ("Console.WriteLine",
@"Console.WriteLine($0);"),

        ["cwl"] = ("Console.WriteLine with text",
@"Console.WriteLine($""$0"");"),

        ["cr"] = ("Console.ReadLine",
@"Console.ReadLine();$0"),

        ["log"] = ("VizConsole.Log",
@"VizConsole.Log($""$0"");"),

        ["mbox"] = ("MessageBox.Show",
@"MessageBox.Show($1""message"", $2""title"", MessageBoxButton.OK, MessageBoxImage.$3Information);$0"),

        // Methods
        ["svm"] = ("Static void Main",
@"public static void Main(string[] args)
{
    $0
}"),

        ["sim"] = ("Static int Main",
@"public static int Main(string[] args)
{
    $0
    return 0;
}"),

        ["method"] = ("Method definition",
@"public void $1MyMethod()
{
    $0
}"),

        ["methodr"] = ("Method with return type",
@"public $1int $2MyMethod()
{
    $0return ;
}"),

        // LINQ
        ["linq"] = ("LINQ query",
@"var $1result = from $2item in $3collection
              where $2item.$4Property > 0
              select $2item;$0"),

        ["linqm"] = ("LINQ method syntax",
@"var $1result = $2collection
    .Where(x => x.$3Property > 0)
    .Select(x => x);$0"),

        // Null Handling
        ["null"] = ("Null check",
@"if ($1variable == null)
{
    throw new ArgumentNullException(nameof($1variable));
}$0"),

        ["nullc"] = ("Null coalescing",
@"$1variable ?? $2defaultValue$0"),

        ["nullca"] = ("Null coalescing assignment",
@"$1variable ??= $2defaultValue;$0"),

        // Testing
        ["testm"] = ("Test method (xUnit)",
@"[Fact]
public void $1TestMethod_$2Scenario_$3ExpectedResult()
{
    // Arrange
    $0
    // Act

    // Assert
}"),

        ["testc"] = ("Test class",
@"public class $1MyClassTests
{
    [Fact]
    public void $2Method_Scenario_ExpectedResult()
    {
        // Arrange
        $0
        // Act

        // Assert
    }
}"),

        // Indexer
        ["indexer"] = ("Indexer property",
@"public $1int this[int $2index]
{
    get { return $3array[$2index]; }
    set { $3array[$2index] = value; }
}$0"),

        // Events
        ["event"] = ("Event declaration",
@"public event $1EventHandler $2MyEvent;$0"),

        ["eventfull"] = ("Event with add/remove",
@"private $1EventHandler _$2myEvent;
public event $1EventHandler $2MyEvent
{
    add { _$2myEvent += value; }
    remove { _$2myEvent -= value; }
}$0"),

        ["invoke"] = ("Invoke event",
@"$1MyEvent?.Invoke(this, $2EventArgs.Empty);$0"),

        // Equality
        ["equals"] = ("Equals override",
@"public override bool Equals(object obj)
{
    if (obj is not $1MyClass other)
        return false;
    return this.$2Property == other.$2Property;
}

public override int GetHashCode()
{
    return $2Property.GetHashCode();
}$0"),

        // Dispose Pattern
        ["dispose"] = ("IDisposable implementation",
@"private bool _disposed = false;

public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this);
}

protected virtual void Dispose(bool disposing)
{
    if (!_disposed)
    {
        if (disposing)
        {
            $0// Dispose managed resources
        }
        _disposed = true;
    }
}"),

        // Attributes
        ["attrib"] = ("Attribute class",
@"[AttributeUsage(AttributeTargets.$1Class | AttributeTargets.$2Method)]
public class $3MyAttribute : Attribute
{
    public string $4Name { get; set; }
}$0"),

        // Region
        ["region"] = ("Region block",
@"#region $1MyRegion
$0
#endregion"),

        // ===== Viz2D Shape Snippets =====

        ["circle"] = ("Create a circle",
@"var $1circle = new VCircle($20, $30, $450);
$1circle.Color = ""$5Cyan"";
$1circle.FillColor = ""Transparent"";
$1circle.Draw();$0"),

        ["vline"] = ("Create a line",
@"var $1line = new VLine($20, $30, $4100, $5100);
$1line.Color = ""$6Lime"";
$1line.Draw();$0"),

        ["vrect"] = ("Create a rectangle",
@"var $1rect = new VRectangle($20, $30, $4100, $550);
$1rect.Color = ""$6Yellow"";
$1rect.FillColor = ""$7DarkGoldenrod"";
$1rect.Draw();$0"),

        ["vpoly"] = ("Create a polygon",
@"var $1poly = new VPolygon(
    new VPoint($20, $30),
    new VPoint($450, $5100),
    new VPoint($6100, $70)
);
$1poly.Color = ""Pink"";
$1poly.FillColor = ""DarkMagenta"";
$1poly.Draw();$0"),

        ["vbezier"] = ("Create a bezier curve",
@"var $1bezier = new VBezier(
    new VPoint($20, $30),    // Start
    new VPoint($425, $550),  // Control 1
    new VPoint($675, $750),  // Control 2
    new VPoint($8100, $90)   // End
);
$1bezier.Color = ""Purple"";
$1bezier.Draw();$0"),

        ["vspline"] = ("Create a smooth spline",
@"var $1spline = new VSpline(
    new VPoint(0, 0),
    new VPoint(25, 50),
    new VPoint(50, 0),
    new VPoint(75, 50),
    new VPoint(100, 0)
);
$1spline.Color = ""$2Violet"";
$1spline.Draw();$0"),

        ["varrow"] = ("Create an arrow",
@"var $1arrow = new VArrow($20, $30, $4100, $50);
$1arrow.Color = ""$6Orange"";
$1arrow.HeadLength = $715;
$1arrow.Draw();$0"),

        ["vdim"] = ("Create a dimension line",
@"var $1dim = new VDimension($20, $30, $4100, $50);
$1dim.Offset = $620;
$1dim.Color = ""$7Yellow"";
$1dim.Draw();$0"),

        ["vgroup"] = ("Create a shape group",
@"var $1shape1 = new VCircle(0, 0, 30);
var $2shape2 = new VCircle(0, 0, 20);
var $3group = new VGroup($1shape1, $2shape2);
$3group.Draw();$0"),

        ["vtext"] = ("Create text label",
@"var $1text = new VText($20, $30, ""$4Hello World"");
$1text.Color = ""$5White"";
$1text.Height = $616;
$1text.Draw();$0"),

        ["vellipse"] = ("Create an ellipse",
@"var $1ellipse = new VEllipse($20, $30, $480, $540);
$1ellipse.Color = ""$6Magenta"";
$1ellipse.FillColor = ""Transparent"";
$1ellipse.Draw();$0"),

        ["varc"] = ("Create an arc",
@"var $1arc = new VArc($20, $30, $450, $50, $690);
$1arc.Color = ""$7Cyan"";
$1arc.Draw();$0"),

        ["vpoint"] = ("Create a point",
@"var $1point = new VPoint($20, $30);
$1point.Color = ""$4White"";
$1point.Draw();$0"),

        // ===== Pattern Snippets =====

        ["shapegrid"] = ("Create a grid of shapes",
@"for (int x = 0; x < $15; x++)
{
    for (int y = 0; y < $25; y++)
    {
        var circle = new VCircle(x * $330, y * $330, $410);
        circle.Color = ""$5Cyan"";
        circle.Draw();
    }
}$0"),

        ["radial"] = ("Create radial pattern",
@"int count = $112;
double radius = $2100;
for (int i = 0; i < count; i++)
{
    double angle = i * 360.0 / count * Math.PI / 180;
    double x = Math.Cos(angle) * radius;
    double y = Math.Sin(angle) * radius;
    var circle = new VCircle(x, y, $310);
    circle.Color = ""$4Lime"";
    circle.Draw();
}$0"),

        ["spiral"] = ("Create a spiral pattern",
@"int points = $1100;
for (int i = 0; i < points; i++)
{
    double angle = i * $20.3;
    double radius = i * $32;
    double x = Math.Cos(angle) * radius;
    double y = Math.Sin(angle) * radius;
    var pt = new VPoint(x, y);
    pt.Color = ""$4Purple"";
    pt.Draw();
}$0"),

        ["star"] = ("Create a star shape",
@"var points = new List<VPoint>();
int starPoints = $15;
double outerRadius = $2100;
double innerRadius = $340;

for (int i = 0; i < starPoints * 2; i++)
{
    double radius = (i % 2 == 0) ? outerRadius : innerRadius;
    double angle = i * Math.PI / starPoints - Math.PI / 2;
    points.Add(new VPoint(Math.Cos(angle) * radius, Math.Sin(angle) * radius));
}

var star = new VPolygon(points);
star.Color = ""$4Gold"";
star.FillColor = ""$5DarkOrange"";
star.Draw();$0"),

        ["wave"] = ("Create a wave pattern",
@"var points = new List<VPoint>();
for (double x = $1-100; x <= $2100; x += $35)
{
    double y = Math.Sin(x * $40.1) * $530;
    points.Add(new VPoint(x, y));
}
var wave = new VSpline(points);
wave.Color = ""$6Aqua"";
wave.Draw();$0")
    };

    /// <summary>
    /// Gets snippet code by trigger name.
    /// </summary>
    public static string? GetSnippet(string trigger)
    {
        return Snippets.TryGetValue(trigger.ToLower(), out var snippet) ? snippet.Code : null;
    }

    /// <summary>
    /// Gets all available snippet triggers and descriptions.
    /// </summary>
    public static IEnumerable<(string Trigger, string Description)> GetAll()
    {
        return Snippets.Select(kvp => (kvp.Key, kvp.Value.Description));
    }
}

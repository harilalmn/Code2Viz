# Product Requirements Document (PRD)
## Viz2d - 2D Geometry Visualizer

### Document Information
- **Version**: 1.0
- **Last Updated**: February 2026
- **Status**: Implemented

---

## 1. Product Overview

### 1.1 Purpose
Viz2d is a desktop application that enables users to visualize 2D geometric shapes by writing and executing C# code. It serves as an educational tool for learning geometry concepts and a prototyping tool for geometric algorithms.

### 1.2 Target Users
- Students learning computational geometry
- Developers prototyping geometric algorithms
- Educators teaching geometry concepts
- Anyone needing quick 2D shape visualization

### 1.3 Key Value Propositions
- **Code-driven visualization**: Write C# code to create shapes programmatically
- **Interactive canvas**: Zoom, pan, and explore geometric constructions
- **Immediate feedback**: Execute code and see results instantly
- **Familiar syntax**: Standard C# with intuitive geometry classes

---

## 2. Functional Requirements

### 2.1 Shape Support

#### 2.1.1 Basic Shapes (P0 - Must Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-001 | Point | Done | Single point marker with coordinates |
| FR-002 | Line | Done | Line segment between two points |
| FR-003 | Circle | Done | Circle with center and radius |
| FR-004 | Arc | Done | Circular arc with start/end angles |

#### 2.1.2 Extended Shapes (P1 - Should Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-005 | Rectangle | Done | Axis-aligned rectangle |
| FR-006 | Ellipse | Done | Ellipse with two radii |
| FR-007 | Polygon | Done | Closed polygon with N vertices |
| FR-008 | Polyline | Done | Open polyline with N vertices |

#### 2.1.3 Future Shapes (P2 - Nice to Have)
| ID | Shape | Status | Description |
|----|-------|--------|-------------|
| FR-009 | Bezier Curve | Done | Cubic Bezier curve |
| FR-010 | Spline | Done | B-spline or Catmull-Rom |
| FR-011 | Text | Done | Text labels on canvas |

### 2.2 Shape Styling

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-020 | Stroke Color | Done | Customizable border color |
| FR-021 | Fill Color | Done | Customizable fill color |
| FR-022 | Stroke Thickness | Done | Customizable line width |
| FR-023 | Dash Pattern | Planned | Dashed/dotted lines |
| FR-024 | Opacity | Done | Transparency support |

### 2.3 Canvas Features

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-030 | Mouse Wheel Zoom | Done | Zoom centered on cursor |
| FR-031 | Middle-Click Pan | Done | Drag to pan view |
| FR-032 | Zoom Extents | Done | Auto-fit all shapes |
| FR-033 | Grid Lines | Done | Toggleable grid display |
| FR-034 | Coordinate Axes | Done | X/Y axes at origin |
| FR-035 | Coordinate Display | Done | Real-time mouse coords |
| FR-036 | Snap to Grid | Done | Snap points to grid (F9 toggle) |

### 2.4 Code Editor

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-040 | Syntax Highlighting | Done | C# syntax colors |
| FR-041 | Line Numbers | Done | Visible line numbers |
| FR-042 | Code Formatting | Done | Auto-format with Ctrl+Shift+F |
| FR-043 | Error Display | Done | Errors shown in footer |
| FR-044 | Autocomplete | Done | IntelliSense for geometry |
| FR-045 | Error Highlighting | Done | Inline error markers |

### 2.5 File Operations

| ID | Feature | Status | Description |
|----|---------|--------|-------------|
| FR-050 | New File | Done | Create new code file |
| FR-051 | Open File | Done | Open existing .cs/.viz files |
| FR-052 | Save File | Done | Save current code |
| FR-053 | Export PNG | Done | Export canvas to PNG |
| FR-054 | Export SVG | Done | Export as vector graphics |

---

## 3. Non-Functional Requirements

### 3.1 Performance
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-001 | Code execution time | < 2 seconds for typical scripts |
| NFR-002 | Canvas redraw | < 100ms for < 1000 shapes |
| NFR-003 | Zoom/Pan responsiveness | < 50ms latency |

### 3.2 Usability
| ID | Requirement | Description |
|----|-------------|-------------|
| NFR-010 | Learning curve | New users productive within 5 minutes |
| NFR-011 | Error messages | Clear, actionable error descriptions |
| NFR-012 | Keyboard shortcuts | Standard shortcuts for common actions |

### 3.3 Compatibility
| ID | Requirement | Status |
|----|-------------|--------|
| NFR-020 | Windows 10/11 | Supported |
| NFR-021 | .NET 8.0 | Required |

---

## 4. User Interface Requirements

### 4.1 Layout
```
┌─────────────────────────────────────────────────────────────┐
│ [New] [Open] [Save] | [Run] [Clear] | [Format] | [Export] □ Grid │  <- Ribbon
├────────────────────────────────┬────────────────────────────┤
│                                │                            │
│                                │    // Code Editor          │
│         Canvas Area            │    Point p = new Point();  │
│         (2/3 width)            │    p.Draw();               │
│                                │                            │
│                                │    (1/3 width)             │
├────────────────────────────────┴────────────────────────────┤
│ Status: Ready              X: 50.00  Y: 25.00    Scroll: Zoom │  <- Footer
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Theme
- Dark theme for canvas area (reduces eye strain)
- Light theme for code editor (better code readability)
- Accent color: Blue (#007ACC)

---

## 5. Technical Architecture

### 5.1 Technology Stack
- **Framework**: WPF (.NET 8.0)
- **Code Editor**: AvalonEdit
- **Script Execution**: Roslyn (Microsoft.CodeAnalysis.CSharp.Scripting)
- **Coordinate System**: Mathematical (Y-up, origin at center)

### 5.2 Key Components
1. **Geometry Module**: Shape classes with Draw() methods
2. **Canvas Module**: Custom WPF canvas with transforms
3. **Editor Module**: Syntax highlighting and formatting
4. **Execution Module**: Roslyn-based C# script runner

---

## 6. Success Metrics

| Metric | Target |
|--------|--------|
| Shape rendering accuracy | 100% geometric correctness |
| Code execution success rate | > 95% for valid code |
| User error recovery | Clear guidance within 1 error message |

---

## 7. Release History

### Version 1.0 (Current)
- Core shapes: Point, Line, Arc, Circle
- Extended shapes: Rectangle, Ellipse, Polygon, Polyline
- Shape styling: Colors, thickness
- Canvas: Zoom, Pan, Grid, Coordinates
- Editor: Syntax highlighting, formatting
- Export: PNG

### Version 1.1 (Planned)
- Bezier curves and splines
- Autocomplete in editor
- SVG export
- Snap to grid

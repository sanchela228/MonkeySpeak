# MonkeySpeak Engine

A declarative XML-based UI framework for .NET 9.0 built on Raylib-cs.

## Features

- **Declarative XML Layouts** - Define UI structure in XML files
- **Flexible Layout System** - Support for absolute, percentage, and auto sizing
- **Grid Layout** - CSS Grid-like container with fractional units
- **Text Rendering** - Word wrapping, alignment, and ellipsis truncation
- **Event Binding** - Connect UI events to methods via expressions
- **Controller Pattern** - Attach logic to nodes without subclassing
- **Custom Window** - Undecorated window with resize, drag, and rounded corners
- **Hot Reload Ready** - XML-based structure supports runtime reloading

## Quick Start

### Minimal Application

```csharp
using Engine;

Window.Init("Index.xml");
Window.Run();
```

### Basic Layout (Index.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Window Title="My Application">
    <Rect Width="100%" Height="50" Color="Blue">
        <Text Content="Hello World" FontFamily="default.ttf,24,White"/>
    </Rect>
</Window>
```

## Layout Basics

### Size Values

```xml
<!-- Absolute pixels -->
<Rect Width="200" Height="100"/>

<!-- Percentage of parent -->
<Rect Width="50%" Height="100%"/>

<!-- Auto-size to content -->
<Rect Width="Auto" Height="Auto"/>
```

### Margins and Centering

```xml
<!-- Fixed margins -->
<Rect Margin="10"/>
<Rect MarginLeft="20" MarginTop="10"/>

<!-- Center horizontally -->
<Rect Width="200" MarginLeft="Auto" MarginRight="Auto"/>

<!-- Center vertically -->
<Rect Height="100" MarginTop="Auto" MarginBottom="Auto"/>

<!-- Push to right -->
<Rect Width="100" MarginLeft="Auto"/>
```

### Positioning

```xml
<!-- Relative (default) - flows with siblings -->
<Rect PositionMode="Relative"/>

<!-- Absolute - positioned relative to parent -->
<Rect PositionMode="Absolute" X="50" Y="50"/>
```

## UI Elements

### Rect

Rectangle with optional rounded corners.

```xml
<Rect 
    Width="200" 
    Height="100" 
    Color="Blue" 
    Rounded="10" 
    Border="2" 
    BorderColor="White"
/>
```

### Circle

Circle element.

```xml
<Circle Width="50" Height="50" Color="Red"/>
```

### Grid

Grid layout container.

```xml
<Grid Columns="3" Gap="10">
    <Rect Color="Red"/>
    <Rect Color="Green"/>
    <Rect Color="Blue"/>
</Grid>

<!-- Advanced column definitions -->
<Grid Columns="100 1fr 2fr" RowGap="5" ColumnGap="10">
    <!-- ... -->
</Grid>
```

### Text

Text with wrapping and alignment.

```xml
<Text 
    Content="Your text here"
    FontFamily="MyFont"
    Wrap="true"
    Align="Center"
    ClampDots="true"
/>
```

## Fonts

### Register a Font

```xml
<FontFamily 
    Name="MyFont" 
    Font="MyFont.ttf" 
    Size="16" 
    Color="White" 
    Spacing="1"
/>
```

### Use in Text

```xml
<Text Content="Hello" FontFamily="MyFont"/>
```

### Inline Font Definition

```xml
<Text Content="Hello" FontFamily="Arial.ttf,24,White"/>
```

## Binding System

Bindings allow you to call methods and use dynamic values in XML attributes. Any attribute value wrapped in `{...}` is treated as a binding expression.

### Where Bindings Work

Bindings can be used in two contexts:

**1. Event Handlers** - Execute method when event fires:
```xml
<Rect OnRelease="{HandleClick()}"/>
<Rect OnPress="{Window.Drag()}"/>
```

**2. Property Values** - Set property from method return value:
```xml
<Text Content="{GetUserName()}"/>
<Rect Width="{CalculateWidth()}"/>
```

### Binding Syntax

```xml
<!-- Instance method (searches hierarchy) -->
{MethodName()}

<!-- Static method on class -->
{ClassName.MethodName()}

<!-- With arguments -->
{SetValue('hello', 123, true)}
```

### Argument Types

- **Strings**: `'text'` or `"text"`
- **Integers**: `123`
- **Floats**: `45.6`
- **Booleans**: `true`, `false`
- **Null**: `null`

### Method Resolution Order

For instance methods (without class prefix), the resolver searches in this order:

1. **Node's Controller** - If node has attached controller, search there first
2. **Node itself** - Search methods on the node
3. **Parent hierarchy** - Walk up parent chain, checking Controller then Node at each level
4. **ParentScene** - Finally check the parent Scene and its Controller

```xml
<!-- Searches: Controller -> Node -> Parent.Controller -> Parent -> ... -> Scene -->
<Rect OnRelease="{HandleClick()}"/>
```

### Static Method Calls

Use `ClassName.MethodName()` syntax to call static methods:

```xml
<!-- Calls static method Window.Close() -->
<Rect OnRelease="{Window.Close()}"/>

<!-- Calls static method on any registered type -->
<Rect OnRelease="{MyHelper.DoSomething()}"/>
```

The resolver scans all loaded assemblies to find the class by name.

### Examples

```xml
<!-- Event: Call controller method -->
<Rect Controller="ButtonController" OnRelease="{OnButtonClick()}"/>

<!-- Event: Call static Window method -->
<Circle OnRelease="{Window.Minimize()}"/>

<!-- Event: Call method with arguments -->
<Rect OnHover="{SetHighlight(true)}" OnHoverExit="{SetHighlight(false)}"/>

<!-- Property: Get value from method -->
<Text Content="{GetLocalizedText('welcome_message')}"/>
```

### Registering Types for Static Calls

Custom types are automatically discovered, but you can explicitly register:

```csharp
BindingResolver.RegisterType<MyUtilityClass>();
BindingResolver.RegisterType<MyUtilityClass>("Utils"); // With alias
```

## Events

### Available Events

- `OnPressed` - Mouse button just pressed (fires once)
- `OnPress` - Mouse button held down (fires every frame)
- `OnRelease` - Mouse button released (fires once)
- `OnHover` - Mouse entered element (fires once)
- `OnHoverExit` - Mouse left element (fires once)

### Cursor

Set cursor when hovering over element:

```xml
<Rect Cursor="4" OnHover="{...}"/>
```

## Window Controls

Built-in window management:

```xml
<!-- Custom title bar with window controls -->
<Rect Width="100%" Height="40" OnPress="{Window.Drag()}">
    <Grid Columns="3" MarginLeft="Auto" Gap="5">
        <Circle OnRelease="{Window.Minimize()}" Color="Yellow"/>
        <Circle OnRelease="{Window.ToggleMaximize()}" Color="Green"/>
        <Circle OnRelease="{Window.Close()}" Color="Red"/>
    </Grid>
</Rect>
```

## Include Files

Split layouts into multiple files:

```xml
<!-- Index.xml -->
<Window Title="App">
    <Include Source="Header.xml"/>
    <Include Source="Content.xml"/>
</Window>
```

### Fragment

Group multiple elements or definitions:

```xml
<!-- Fonts.xml -->
<Fragment>
    <FontFamily Name="Body" Font="Regular.ttf" Size="14"/>
    <FontFamily Name="Title" Font="Bold.ttf" Size="24"/>
</Fragment>
```

## Controllers

Attach logic to nodes:

```csharp
public class MyController : NodeController
{
    public override void OnBind()
    {
        // Initialization
    }
    
    public override void OnUpdate(float deltaTime)
    {
        // Per-frame logic
    }
    
    public void ButtonClicked()
    {
        Console.WriteLine("Clicked!");
    }
}
```

```xml
<Rect Controller="MyController" OnRelease="{ButtonClicked()}"/>
```

## Custom Elements

Create reusable components:

```csharp
public class Button : Node
{
    public string Label { get; set; } = "";
    public Color Color { get; set; } = Color.Gray;
    
    public override void Update(float deltaTime) { }
    
    public override void Draw()
    {
        Raylib.DrawRectangle(
            (int)ComputedPosition.X,
            (int)ComputedPosition.Y,
            (int)ComputedWidth,
            (int)ComputedHeight,
            Color
        );
    }
    
    public override void Dispose() { }
}

// Register before Window.Init()
XmlLoader.RegisterElement<Button>("Button");
```

```xml
<Button Label="Click Me" Color="Blue" Width="100" Height="40"/>
```

## Colors

Supported color formats:

```xml
<!-- Named colors -->
<Rect Color="Red"/>
<Rect Color="Blue"/>
<Rect Color="Blank"/>

<!-- Hex colors -->
<Rect Color="#FF5500"/>
<Rect Color="#FF550080"/>
```

Named colors: `Red`, `Green`, `Blue`, `White`, `Black`, `Yellow`, `Orange`, `Purple`, `Gray`, `Blank`

## Requirements

- .NET 9.0
- Raylib-cs

## Documentation

See [tech.md](tech.md) for detailed technical documentation.

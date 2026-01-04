using System.Numerics;
using Raylib_cs;

namespace Engine.UI;

public class Grid : Node
{
    public string Columns { get; set; } = "1";
    public string Rows { get; set; } = "auto";
    
    public float Gap { get; set; } = 0;
    public float ColumnGap { get; set; } = -1;
    public float RowGap { get; set; } = -1;
    
    private float[] _columnWidths = Array.Empty<float>();
    private float[] _rowHeights = Array.Empty<float>();
    private int _columnCount = 1;
    
    public override void Update(float deltaTime) { }
    
    public override void Draw()
    {
        if (Color.A != 0)
        {
            Raylib.DrawRectangle(
                (int)ComputedPosition.X,
                (int)ComputedPosition.Y,
                (int)ComputedWidth,
                (int)ComputedHeight,
                Color
            );
        }
    }
    
    public override void Dispose() { }
    
    public Color Color { get; set; } = Color.Blank;
    
    private struct GridTrack
    {
        public float Value;
        public bool IsFr;
        public bool IsAuto;
    }
    
    private GridTrack[] ParseTracks(string definition)
    {
        var trimmed = definition.Trim();
        
        if (int.TryParse(trimmed, out int count))
        {
            var tracks = new GridTrack[count];
            for (int i = 0; i < count; i++)
                tracks[i] = new GridTrack { Value = 1, IsFr = true };
            return tracks;
        }
        
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new GridTrack[parts.Length];
        
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim().ToLower();
            
            if (part == "auto")
            {
                result[i] = new GridTrack { IsAuto = true };
            }
            else if (part.EndsWith("fr"))
            {
                var frValue = part.Replace("fr", "");
                result[i] = new GridTrack 
                { 
                    Value = float.TryParse(frValue, out float fr) ? fr : 1, 
                    IsFr = true 
                };
            }
            else
            {
                result[i] = new GridTrack 
                { 
                    Value = float.TryParse(part, out float px) ? px : 0 
                };
            }
        }
        
        return result;
    }
    
    public override void MeasureSize()
    {
        float parentWidth = Parent?.ComputedWidth ?? 800;
        float parentHeight = Parent?.ComputedHeight ?? 600;
        
        if (Parent != null)
        {
            parentWidth -= Parent.ComputedBorderLeft + Parent.ComputedBorderRight 
                         + Parent.ComputedPaddingLeft + Parent.ComputedPaddingRight;
            parentHeight -= Parent.ComputedBorderTop + Parent.ComputedBorderBottom 
                          + Parent.ComputedPaddingTop + Parent.ComputedPaddingBottom;
        }
        
        ResolveSize(parentWidth, parentHeight);
        
        var columnTracks = ParseTracks(Columns);
        _columnCount = columnTracks.Length;
        
        float effectiveColumnGap = ColumnGap >= 0 ? ColumnGap : Gap;
        float effectiveRowGap = RowGap >= 0 ? RowGap : Gap;
        
        var relativeChildren = _children.Where(c => c.PositionMode == PositionMode.Relative).ToList();
        
        if (Width.IsAuto)
        {
            foreach (var child in relativeChildren)
                child.MeasureSize();
            
            float[] columnMaxWidths = new float[_columnCount];
            for (int i = 0; i < relativeChildren.Count; i++)
            {
                var child = relativeChildren[i];
                int col = i % _columnCount;
                float childW = child.ComputedMarginLeft + child.ComputedWidth + child.ComputedMarginRight;
                if (childW > columnMaxWidths[col])
                    columnMaxWidths[col] = childW;
            }
            
            float totalWidth = columnMaxWidths.Sum() + effectiveColumnGap * (_columnCount - 1);
            ComputedWidth = totalWidth + ComputedBorderLeft + ComputedBorderRight 
                          + ComputedPaddingLeft + ComputedPaddingRight;
            _columnWidths = columnMaxWidths;
        }
        
        float contentWidth = ComputedWidth - ComputedBorderLeft - ComputedBorderRight 
                           - ComputedPaddingLeft - ComputedPaddingRight;
        float totalColumnGaps = effectiveColumnGap * (_columnCount - 1);
        float availableWidth = contentWidth - totalColumnGaps;
        
        float fixedWidth = 0;
        float totalFr = 0;
        
        foreach (var track in columnTracks)
        {
            if (track.IsFr)
                totalFr += track.Value;
            else if (!track.IsAuto)
                fixedWidth += track.Value;
        }
        
        float frUnit = totalFr > 0 ? (availableWidth - fixedWidth) / totalFr : 0;
        
        if (!Width.IsAuto)
        {
            _columnWidths = new float[_columnCount];
            for (int i = 0; i < _columnCount; i++)
            {
                if (columnTracks[i].IsFr)
                    _columnWidths[i] = frUnit * columnTracks[i].Value;
                else if (columnTracks[i].IsAuto)
                    _columnWidths[i] = 0;
                else
                    _columnWidths[i] = columnTracks[i].Value;
            }
        }
        
        int rowCount = (int)Math.Ceiling((double)relativeChildren.Count / _columnCount);
        if (rowCount == 0) rowCount = 1;
        
        var rowTracks = ParseTracks(Rows);
        _rowHeights = new float[rowCount];
        
        for (int i = 0; i < relativeChildren.Count; i++)
        {
            var child = relativeChildren[i];
            int col = i % _columnCount;
            float cellWidth = _columnWidths[col];
            
            child.ResolveSize(cellWidth, ComputedHeight);
            
            foreach (var grandChild in child.Children)
                grandChild.MeasureSize();
        }
        
        foreach (var child in _children)
        {
            if (child.PositionMode == PositionMode.Absolute)
                child.MeasureSize();
        }
        
        if (Height.IsAuto)
        {
            for (int i = 0; i < relativeChildren.Count; i++)
            {
                var child = relativeChildren[i];
                int row = i / _columnCount;
                
                float childTotalHeight = child.ComputedMarginTop + child.ComputedHeight + child.ComputedMarginBottom;
                if (childTotalHeight > _rowHeights[row])
                    _rowHeights[row] = childTotalHeight;
            }
            
            for (int row = 0; row < rowCount; row++)
            {
                if (row < rowTracks.Length && !rowTracks[row].IsAuto && !rowTracks[row].IsFr)
                {
                    _rowHeights[row] = rowTracks[row].Value;
                }
            }
            
            float totalHeight = _rowHeights.Sum() + effectiveRowGap * (rowCount - 1);
            ComputedHeight = totalHeight + ComputedBorderTop + ComputedBorderBottom 
                           + ComputedPaddingTop + ComputedPaddingBottom;
        }
        else
        {
            float contentHeight = ComputedHeight - ComputedBorderTop - ComputedBorderBottom 
                                - ComputedPaddingTop - ComputedPaddingBottom;
            float totalRowGaps = effectiveRowGap * (rowCount - 1);
            float availableHeight = contentHeight - totalRowGaps;
            float rowHeight = availableHeight / rowCount;
            
            for (int row = 0; row < rowCount; row++)
                _rowHeights[row] = rowHeight;
        }
    }
    
    public override void ArrangeChildren()
    {
        float offsetX = ComputedBorderLeft + ComputedPaddingLeft;
        float offsetY = ComputedBorderTop + ComputedPaddingTop;
        
        float effectiveColumnGap = ColumnGap >= 0 ? ColumnGap : Gap;
        float effectiveRowGap = RowGap >= 0 ? RowGap : Gap;
        
        float contentWidth = ComputedWidth - ComputedBorderLeft - ComputedBorderRight 
                           - ComputedPaddingLeft - ComputedPaddingRight;
        float contentHeight = ComputedHeight - ComputedBorderTop - ComputedBorderBottom 
                            - ComputedPaddingTop - ComputedPaddingBottom;
        
        var relativeChildren = _children.Where(c => c.PositionMode == PositionMode.Relative).ToList();
        
        float currentY = 0;
        
        for (int i = 0; i < relativeChildren.Count; i++)
        {
            var child = relativeChildren[i];
            int col = i % _columnCount;
            int row = i / _columnCount;
            
            if (col == 0 && row > 0)
                currentY += _rowHeights[row - 1] + effectiveRowGap;
            
            float cellX = 0;
            for (int c = 0; c < col; c++)
                cellX += _columnWidths[c] + effectiveColumnGap;
            
            float cellWidth = _columnWidths[col];
            float cellHeight = _rowHeights[row];
            
            ResolveAutoMargins(child, cellWidth, cellHeight);
            
            child.ComputedPosition = new Vector2(
                ComputedPosition.X + offsetX + cellX + child.ComputedMarginLeft,
                ComputedPosition.Y + offsetY + currentY + child.ComputedMarginTop
            );
            
            child.ArrangeChildren();
        }
        
        foreach (var child in _children)
        {
            if (child.PositionMode == PositionMode.Absolute)
            {
                ResolveAutoMargins(child, contentWidth, contentHeight);
                child.ComputedPosition = ComputedPosition + child.Position 
                    + new Vector2(offsetX + child.ComputedMarginLeft, offsetY + child.ComputedMarginTop);
                child.ArrangeChildren();
            }
        }
    }
}

namespace Engine.Types;

public struct DynamicFloat
{
    public enum ValueType
    {
        Absolute,
        Percent,
        Auto
    }
    
    public float Value { get; set; }
    public ValueType Type { get; set; }
    
    public DynamicFloat(float value, ValueType type = ValueType.Absolute)
    {
        Value = value;
        Type = type;
    }
    
    public float Resolve(float parentSize)
    {
        return Type switch
        {
            ValueType.Percent => parentSize * Value / 100f,
            _ => Value
        };
    }
    
    public bool IsZero => Value == 0 && Type != ValueType.Auto;
    public bool IsPercent => Type == ValueType.Percent;
    public bool IsAuto => Type == ValueType.Auto;
    
    public static DynamicFloat Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new DynamicFloat(0, ValueType.Absolute);
        
        input = input.Trim();
        
        if (input.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return new DynamicFloat(0, ValueType.Auto);
        
        if (input.EndsWith("%"))
        {
            var numPart = input[..^1];
            if (float.TryParse(numPart, out var percent))
                return new DynamicFloat(percent, ValueType.Percent);
        }
        
        if (float.TryParse(input, out var value))
            return new DynamicFloat(value, ValueType.Absolute);
        
        return new DynamicFloat(0, ValueType.Absolute);
    }
    
    public static implicit operator DynamicFloat(float value) => new(value, ValueType.Absolute);
    
    public override string ToString() => Type switch
    {
        ValueType.Percent => $"{Value}%",
        ValueType.Auto => "Auto",
        _ => Value.ToString()
    };
}

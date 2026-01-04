using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Engine.Binding;

public static class BindingResolver
{
    private static readonly Dictionary<string, Type> _typeCache = new();
    private static bool _typesScanned = false;
    
    public static bool IsBinding(string? value) => value != null && value.StartsWith("{") && value.EndsWith("}");
    public static string ExtractBinding(string value) => value[1..^1].Trim();
    
    public static void Execute(string binding, Node context)
    {
        if (!IsBinding(binding)) return;
        
        var expression = ExtractBinding(binding);
        ExecuteExpression(expression, context);
    }
    
    public static object? ExecuteWithReturn(string binding, Node context)
    {
        if (!IsBinding(binding)) return null;
        
        var expression = ExtractBinding(binding);
        return ExecuteExpression(expression, context);
    }
    
    private static object? ExecuteExpression(string expression, Node context)
    {
        var (target, methodName, args) = ParseMethodCall(expression);
        
        if (target != null)
        {
            return ExecuteStatic(target, methodName, args);
        }
        else
        {
            return ExecuteOnHierarchy(methodName, args, context);
        }
    }
    
    private static (string? target, string methodName, object?[] args) ParseMethodCall(string expression)
    {
        var match = Regex.Match(expression, @"^(?:(\w+)\.)?(\w+)\((.*)\)$");
        
        if (!match.Success)
        {
            Console.WriteLine($"Invalid binding expression: {expression}");
            return (null, expression, Array.Empty<object?>());
        }
        
        var target = match.Groups[1].Success ? match.Groups[1].Value : null;
        var methodName = match.Groups[2].Value;
        var argsString = match.Groups[3].Value.Trim();
        
        var args = ParseArguments(argsString);
        
        return (target, methodName, args);
    }
    
    private static object?[] ParseArguments(string argsString)
    {
        if (string.IsNullOrWhiteSpace(argsString))
            return Array.Empty<object?>();
        
        var args = new List<object?>();
        var currentArg = "";
        var inString = false;
        var stringChar = ' ';
        var depth = 0;
        
        for (int i = 0; i < argsString.Length; i++)
        {
            var c = argsString[i];
            
            if (!inString && (c == '"' || c == '\''))
            {
                inString = true;
                stringChar = c;
                currentArg += c;
            }
            else if (inString && c == stringChar)
            {
                inString = false;
                currentArg += c;
            }
            else if (!inString && c == '(')
            {
                depth++;
                currentArg += c;
            }
            else if (!inString && c == ')')
            {
                depth--;
                currentArg += c;
            }
            else if (!inString && c == ',' && depth == 0)
            {
                args.Add(ParseSingleArgument(currentArg.Trim()));
                currentArg = "";
            }
            else
            {
                currentArg += c;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(currentArg))
            args.Add(ParseSingleArgument(currentArg.Trim()));
        
        return args.ToArray();
    }
    
    private static object? ParseSingleArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return null;
        
        if ((arg.StartsWith("\"") && arg.EndsWith("\"")) || 
            (arg.StartsWith("'") && arg.EndsWith("'")))
            return arg[1..^1];
        
        if (arg.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        
        if (arg.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;
        
        if (arg.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        
        if (int.TryParse(arg, out var intVal))
            return intVal;
        
        if (float.TryParse(arg, out var floatVal))
            return floatVal;
        
        return arg;
    }
    
    private static object? ExecuteOnHierarchy(string methodName, object?[] args, Node context)
    {
        var method = FindMethod(context.GetType(), methodName, args);
        if (method != null)
            return InvokeMethod(method, context, args);
        
        var parent = context.Parent;
        while (parent != null)
        {
            method = FindMethod(parent.GetType(), methodName, args);
            if (method != null)
                return InvokeMethod(method, parent, args);
            parent = parent.Parent;
        }
        
        var scene = context.ParentScene;
        if (scene != null && scene != context)
        {
            method = FindMethod(scene.GetType(), methodName, args);
            if (method != null)
                return InvokeMethod(method, scene, args);
        }
        
        Console.WriteLine($"Method '{methodName}' not found in hierarchy");
        return null;
    }
    
    private static object? ExecuteStatic(string className, string methodName, object?[] args)
    {
        var type = FindType(className);
        if (type == null)
        {
            Console.WriteLine($"Type '{className}' not found");
            return null;
        }
        
        var method = FindMethod(type, methodName, args, BindingFlags.Public | BindingFlags.Static);
        if (method != null)
            return InvokeMethod(method, null, args);
        
        Console.WriteLine($"Static method '{methodName}' not found on '{className}'");
        return null;
    }
    
    private static MethodInfo? FindMethod(Type type, string methodName, object?[] args, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        var methods = type.GetMethods(flags)
            .Where(m => m.Name == methodName)
            .ToList();
        
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == args.Length)
            {
                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args[i] != null && !parameters[i].ParameterType.IsAssignableFrom(args[i]!.GetType()))
                    {
                        if (!TryConvert(args[i], parameters[i].ParameterType, out _))
                        {
                            match = false;
                            break;
                        }
                    }
                }
                if (match) return method;
            }
        }
        
        var noArgsMethod = methods.FirstOrDefault(m => m.GetParameters().Length == 0);
        if (noArgsMethod != null && args.Length == 0)
            return noArgsMethod;
        
        return methods.FirstOrDefault();
    }
    
    private static object? InvokeMethod(MethodInfo method, object? target, object?[] args)
    {
        try
        {
            var parameters = method.GetParameters();
            var convertedArgs = new object?[parameters.Length];
            
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < args.Length)
                {
                    if (TryConvert(args[i], parameters[i].ParameterType, out var converted))
                        convertedArgs[i] = converted;
                    else
                        convertedArgs[i] = args[i];
                }
                else if (parameters[i].HasDefaultValue)
                {
                    convertedArgs[i] = parameters[i].DefaultValue;
                }
            }
            
            return method.Invoke(target, convertedArgs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error invoking method '{method.Name}': {ex.Message}");
            return null;
        }
    }
    
    private static bool TryConvert(object? value, Type targetType, out object? result)
    {
        result = null;
        if (value == null) return true;
        
        try
        {
            if (targetType == typeof(float) && value is int intVal)
            {
                result = (float)intVal;
                return true;
            }
            
            if (targetType == typeof(int) && value is float floatVal)
            {
                result = (int)floatVal;
                return true;
            }
            
            result = Convert.ChangeType(value, targetType);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static Type? FindType(string className)
    {
        if (_typeCache.TryGetValue(className, out var cached))
            return cached;
        
        if (!_typesScanned)
            ScanTypes();
        
        return _typeCache.GetValueOrDefault(className);
    }
    
    private static void ScanTypes()
    {
        _typesScanned = true;
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!_typeCache.ContainsKey(type.Name))
                        _typeCache[type.Name] = type;
                    
                    if (!_typeCache.ContainsKey(type.FullName ?? type.Name))
                        _typeCache[type.FullName ?? type.Name] = type;
                }
            }
            catch
            {
            }
        }
    }
    
    public static void RegisterType<T>(string? alias = null)
    {
        var type = typeof(T);
        _typeCache[alias ?? type.Name] = type;
    }
}

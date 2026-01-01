using System.Reflection;
using Fluxor;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Tests;

/// <summary>
/// Tests to verify that all Fluxor reducers have the correct signature.
/// This catches configuration errors at test time instead of runtime.
/// </summary>
public class FluxorReducerSignatureTests
{
    private static Assembly GetWebAssembly() => typeof(SessionState).Assembly;

    [Fact]
    public void AllReducerMethods_ShouldHaveTwoParameters()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 2)
            {
                invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} has {parameters.Length} parameters (expected 2)");
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerMethods_ShouldHaveStateAsFirstParameter()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length >= 1)
            {
                var firstParam = parameters[0];
                var paramType = firstParam.ParameterType;
                
                // Check if it's a state type (should end with "State" or be a record)
                if (!paramType.Name.EndsWith("State"))
                {
                    invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} first parameter '{firstParam.Name}' is type '{paramType.Name}' (expected a State type)");
                }
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerMethods_ShouldHaveActionAsSecondParameter()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length >= 2)
            {
                var secondParam = parameters[1];
                var paramType = secondParam.ParameterType;
                
                // Check if it's an action type (should end with "Action")
                if (!paramType.Name.EndsWith("Action"))
                {
                    invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} second parameter '{secondParam.Name}' is type '{paramType.Name}' (expected an Action type)");
                }
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerMethods_ShouldBeStatic()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            if (!method.IsStatic)
            {
                invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} is not static");
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerMethods_ShouldReturnStateType()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            var returnType = method.ReturnType;
            
            // Check if return type ends with "State"
            if (!returnType.Name.EndsWith("State"))
            {
                invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} returns '{returnType.Name}' (expected a State type)");
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerMethods_FirstAndReturnTypeShouldMatch()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerMethods = GetAllReducerMethods(assembly);
        var invalidMethods = new List<string>();

        // Act
        foreach (var method in reducerMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length >= 1)
            {
                var firstParamType = parameters[0].ParameterType;
                var returnType = method.ReturnType;
                
                if (firstParamType != returnType)
                {
                    invalidMethods.Add($"{method.DeclaringType?.Name}.{method.Name} takes '{firstParamType.Name}' but returns '{returnType.Name}' (they should match)");
                }
            }
        }

        // Assert
        Assert.Empty(invalidMethods);
    }

    [Fact]
    public void AllReducerClasses_ShouldBeStatic()
    {
        // Arrange
        var assembly = GetWebAssembly();
        var reducerClasses = assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Reducers") && t.IsClass)
            .ToList();
        
        var invalidClasses = new List<string>();

        // Act
        foreach (var reducerClass in reducerClasses)
        {
            if (!reducerClass.IsAbstract || !reducerClass.IsSealed)
            {
                invalidClasses.Add($"{reducerClass.Name} is not static (should be static class)");
            }
        }

        // Assert
        Assert.Empty(invalidClasses);
    }

    /// <summary>
    /// Helper method to get all methods decorated with [ReducerMethod] attribute
    /// </summary>
    private static IEnumerable<MethodInfo> GetAllReducerMethods(Assembly assembly)
    {
        var reducerMethodAttributeType = typeof(ReducerMethodAttribute);
        
        return assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Where(m => m.GetCustomAttributes(reducerMethodAttributeType, false).Any())
            .ToList();
    }
}

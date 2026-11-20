using System.Reflection;
using SmartLogistics.Core.Planning;

namespace SmartLogistics.Tests.Support;

/// <summary>
/// Finds the solver by reflection so the test project compiles before the solver exists (docs/odluke.md T14):
/// a public, non-abstract class in the Core assembly that implements <see cref="IRoutePlanner"/> and has a
/// parameterless constructor. If several exist, the first by full name is used (deterministic).
/// </summary>
internal static class PlannerLocator
{
    public static IRoutePlanner Create()
    {
        var type = typeof(IRoutePlanner).Assembly
            .GetTypes()
            .Where(t => t.IsPublic
                && t.IsClass
                && !t.IsAbstract
                && typeof(IRoutePlanner).IsAssignableFrom(t)
                && t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

        if (type is null)
        {
            Assert.Fail("No IRoutePlanner implementation found in SmartLogistics.Core");
        }

        return (IRoutePlanner)Activator.CreateInstance(type)!;
    }
}

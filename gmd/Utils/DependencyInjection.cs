using System.Reflection;
using Autofac;
using Autofac.Core.Activators.Reflection;

namespace gmd.Utils;


/// <summary>
/// Attribute used to mark types that should be registered as a single instance in
/// dependency injection.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public sealed class SingleInstanceAttribute : Attribute
{
}

/// <summary>
/// Wrapper for Autofac dependency injection handler. 
/// </summary>
internal class DependencyInjection
{
    private IContainer? container;

#pragma warning disable CS8714, CS8604
    public T Resolve<T>() => container.Resolve<T>();
#pragma warning restore CS8714, CS8604

    public void RegisterDependencyInjectionTypes()
    {
        try
        {
            ContainerBuilder builder = new ContainerBuilder();

            // Need to make Autofac find also "internal" constructors e.g. windows dialogs
            DefaultConstructorFinder constructorFinder = new DefaultConstructorFinder(
                type => type.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            // Register single instance types
            builder.RegisterAssemblyTypes(executingAssembly)
                .Where(IsSingleInstance)
                .FindConstructorsWith(constructorFinder)
                .AsSelf()
                .AsImplementedInterfaces()
                .SingleInstance()
                .OwnedByLifetimeScope();

            // Register non single instance types
            builder.RegisterAssemblyTypes(executingAssembly)
                .Where(t => !IsSingleInstance(t))
                .FindConstructorsWith(constructorFinder)
                .AsSelf()
                .AsImplementedInterfaces()
                .OwnedByLifetimeScope();

            container = builder.Build();
        }
        catch (Exception e)
        {
            Log.Exception(e, "Failed to register types");
            throw;
        }
    }


    // Returns true if type is marked with the "SingleInstance" attribute
    private static bool IsSingleInstance(Type type) =>
        type.GetCustomAttributes(false)
            .FirstOrDefault(obj => obj.GetType().Name == "SingleInstanceAttribute") != null;
}

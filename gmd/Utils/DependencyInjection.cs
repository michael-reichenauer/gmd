using System.Reflection;
using Autofac;
using Autofac.Core.Activators.Reflection;

namespace gmd.Utils;


// Attribute used to mark types that should be registered as a single instance in
// dependency injection.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public sealed class SingleInstanceAttribute : Attribute
{
}


// Wrapper for Autofac dependency injection handler. 
internal class DependencyInjection
{
    IContainer container = null!;

    public T Resolve<T>() where T : notnull => container.Resolve<T>();

    public void RegisterAllAssemblyTypes()
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

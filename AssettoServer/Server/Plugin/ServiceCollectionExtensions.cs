using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.Plugin;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSingletonHostedService<T>(this IServiceCollection services)
        where T : class, IHostedService
    {
        services.AddSingleton<T>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<T>());
        return services;
    }

    public static IServiceCollection AddAutoActivatedSingleton<T>(this IServiceCollection services)
        where T : class
    {
        services.AddSingleton<T>();
        services.AddSingleton<IAutoActivatedService>(provider => new AutoActivatedService<T>(provider));
        return services;
    }

    public static void ActivateSingletons(this IServiceProvider provider)
    {
        foreach (var service in provider.GetServices<IAutoActivatedService>())
        {
            service.Activate();
        }
    }

    // Func arguments match by type; named delegates also support repeated types, such as model/skin.
    public static IServiceCollection AddTransientFactory<TFactory>(this IServiceCollection services)
        where TFactory : Delegate
    {
        var invoke = typeof(TFactory).GetMethod("Invoke")!;
        var factoryParameters = invoke.GetParameters();
        var matchByType = typeof(TFactory).IsGenericType
                          && typeof(TFactory).GetGenericTypeDefinition().FullName!.StartsWith("System.Func`",
                              StringComparison.Ordinal);
        var constructors = invoke.ReturnType.GetConstructors()
            .Where(constructor => factoryParameters.All(argument => constructor.GetParameters().Count(parameter =>
                Matches(argument, parameter)) == 1))
            .ToArray();
        if (constructors.Length != 1)
        {
            throw new ArgumentException(
                $"Factory {typeof(TFactory)} must match exactly one public constructor of {invoke.ReturnType}.");
        }

        var provider = Expression.Parameter(typeof(IServiceProvider), "provider");
        var arguments = factoryParameters
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name)).ToArray();
        var constructorArguments = constructors[0].GetParameters().Select(parameter =>
        {
            var index = Array.FindIndex(factoryParameters, argument => Matches(argument, parameter));
            if (index >= 0) return (Expression)arguments[index];

            if (parameter.HasDefaultValue)
            {
                var defaultValue = parameter.DefaultValue == null
                    ? (Expression)Expression.Default(parameter.ParameterType)
                    : Expression.Convert(Expression.Constant(parameter.DefaultValue), parameter.ParameterType);
                return Expression.Convert(Expression.Coalesce(
                        Expression.Call(provider, nameof(IServiceProvider.GetService), null,
                            Expression.Constant(parameter.ParameterType)),
                        Expression.Convert(defaultValue, typeof(object))),
                    parameter.ParameterType);
            }

            return Expression.Call(typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetRequiredService),
                [parameter.ParameterType], provider);
        }).ToArray();

        Expression instance = Expression.New(constructors[0], constructorArguments);
        var trackDisposal = typeof(IDisposable).IsAssignableFrom(invoke.ReturnType) ||
                            typeof(IAsyncDisposable).IsAssignableFrom(invoke.ReturnType);
        if (trackDisposal)
        {
            services.TryAddTransient<FactoryLifetime>();
            // Capture after construction, including dependencies created by nested factory calls.
            instance = Expression.Convert(Expression.Call(
                typeof(ServiceCollectionExtensions).GetMethod(nameof(TrackProduct),
                    BindingFlags.Static | BindingFlags.NonPublic)!,
                provider, instance), invoke.ReturnType);
        }

        var factory = Expression.Lambda<Func<IServiceProvider, TFactory>>(
            Expression.Lambda<TFactory>(instance, arguments), provider).Compile();
        services.AddTransient(factory);
        return services;

        bool Matches(ParameterInfo argument, ParameterInfo parameter) =>
            argument.ParameterType == parameter.ParameterType && (matchByType || argument.Name == parameter.Name);
    }

    private static object TrackProduct(IServiceProvider provider, object instance)
    {
        FactoryLifetime lifetime;
        try
        {
            lifetime = provider.GetRequiredService<FactoryLifetime>();
        }
        catch (ObjectDisposedException)
        {
            DisposeProduct(instance);
            throw;
        }

        return lifetime.Capture(instance);
    }

    private static void DisposeProduct(object instance)
    {
        if (instance is IDisposable disposable)
            disposable.Dispose();
        else
            ((IAsyncDisposable)instance).DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private interface IAutoActivatedService
    {
        void Activate();
    }

    private sealed class AutoActivatedService<T>(IServiceProvider provider) : IAutoActivatedService where T : class
    {
        public void Activate() => provider.GetRequiredService<T>();
    }

    private sealed class FactoryLifetime : IDisposable, IAsyncDisposable
    {
        private readonly object _lock = new();
        private object? _instance;
        private bool _disposed;

        public object Capture(object instance)
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _instance = instance;
                    return instance;
                }
            }

            DisposeProduct(instance);
            throw new ObjectDisposedException(nameof(FactoryLifetime));
        }

        public void Dispose()
        {
            var instance = TakeInstance();
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (instance != null)
            {
                throw new InvalidOperationException($"{instance.GetType()} requires asynchronous disposal.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            var instance = TakeInstance();
            if (instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private object? TakeInstance()
        {
            lock (_lock)
            {
                if (_disposed) return null;
                _disposed = true;
                var instance = _instance;
                _instance = null;
                return instance;
            }
        }
    }
}

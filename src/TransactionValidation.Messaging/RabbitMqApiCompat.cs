#nullable enable

using System.Reflection;
using RabbitMQ.Client;

namespace TransactionValidation.Messaging;

/// <summary>
/// Provides reflection-based compatibility helpers for RabbitMQ client APIs that differ across library versions.
/// </summary>
public static class RabbitMqApiCompat
{
    /// <summary>
    /// Creates a RabbitMQ connection using the best available API shape on the current RabbitMQ client version.
    /// </summary>
    /// <param name="hostName">RabbitMQ broker host name.</param>
    /// <param name="port">RabbitMQ broker port.</param>
    /// <param name="userName">RabbitMQ username.</param>
    /// <param name="password">RabbitMQ password.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns>A connection object that can be consumed by compatibility helper methods.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no compatible connection creation API is available.</exception>
    public static async Task<object> CreateConnectionAsync(string hostName, int port, string userName, string password, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password
        };

        var asyncResult = await TryInvokeWithResultAsync(factory, "CreateConnectionAsync", cancellationToken);
        if (asyncResult.found && asyncResult.result is not null)
        {
            return asyncResult.result;
        }

        asyncResult = await TryInvokeWithResultAsync(factory, "CreateConnectionAsync");
        if (asyncResult.found && asyncResult.result is not null)
        {
            return asyncResult.result;
        }

        var syncResult = await TryInvokeWithResultAsync(factory, "CreateConnection");
        if (syncResult.found && syncResult.result is not null)
        {
            return syncResult.result;
        }

        throw new InvalidOperationException("Unable to create RabbitMQ connection using the available client API.");
    }

    /// <summary>
    /// Creates a RabbitMQ channel/model using the best available API shape on the current RabbitMQ client version.
    /// </summary>
    /// <param name="connection">The connection object returned by <see cref="CreateConnectionAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token used by async API variants when supported.</param>
    /// <returns>A channel/model object that can be consumed by compatibility helper methods.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no compatible channel/model creation API is available.</exception>
    public static async Task<object> CreateChannelAsync(object connection, CancellationToken cancellationToken)
    {
        var asyncResult = await TryInvokeWithResultAsync(connection, "CreateChannelAsync", cancellationToken);
        if (asyncResult.found && asyncResult.result is not null)
        {
            return asyncResult.result;
        }

        asyncResult = await TryInvokeWithResultAsync(connection, "CreateChannelAsync");
        if (asyncResult.found && asyncResult.result is not null)
        {
            return asyncResult.result;
        }

        var modelResult = await TryInvokeWithResultAsync(connection, "CreateModel");
        if (modelResult.found && modelResult.result is not null)
        {
            return modelResult.result;
        }

        throw new InvalidOperationException("Unable to create RabbitMQ channel using the available client API.");
    }

    /// <summary>
    /// Attempts to invoke a method on the specified target using compatible signature matching.
    /// </summary>
    /// <param name="target">Target instance containing the method.</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="args">Invocation arguments.</param>
    /// <returns><see langword="true"/> when a compatible method is found and invoked; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> TryInvokeAsync(object target, string methodName, params object?[] args)
    {
        var method = FindCompatibleMethod(target.GetType(), methodName, args);
        if (method is null)
        {
            return false;
        }

        if (!TryResolveInvocableMethod(method, args, out var invocableMethod, out var invokeArgs))
        {
            return false;
        }

        var invokeResult = invocableMethod.Invoke(target, invokeArgs);
        if (invokeResult is Task task)
        {
            await task.ConfigureAwait(false);
            return true;
        }

        if (invokeResult is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return true;
        }

        if (invokeResult is not null && invokeResult.GetType().IsGenericType && invokeResult.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = invokeResult.GetType().GetMethod("AsTask", Type.EmptyTypes)?.Invoke(invokeResult, null) as Task;
            if (asTask is not null)
            {
                await asTask.ConfigureAwait(false);
            }
            return true;
        }

        return true;
    }

    /// <summary>
    /// Attempts to invoke a method and capture its return value using compatible signature matching.
    /// </summary>
    /// <param name="target">Target instance containing the method.</param>
    /// <param name="methodName">Method name to invoke.</param>
    /// <param name="args">Invocation arguments.</param>
    /// <returns>
    /// A tuple where <c>found</c> indicates whether a compatible method was invoked,
    /// and <c>result</c> contains the return value (or task result) when available.
    /// </returns>
    public static async Task<(bool found, object? result)> TryInvokeWithResultAsync(object target, string methodName, params object?[] args)
    {
        var method = FindCompatibleMethod(target.GetType(), methodName, args);
        if (method is null)
        {
            return (false, null);
        }

        if (!TryResolveInvocableMethod(method, args, out var invocableMethod, out var invokeArgs))
        {
            return (false, null);
        }

        var invokeResult = invocableMethod.Invoke(target, invokeArgs);
        if (invokeResult is Task task)
        {
            await task.ConfigureAwait(false);

            if (task.GetType().IsGenericType)
            {
                return (true, task.GetType().GetProperty("Result")?.GetValue(task));
            }

            return (true, null);
        }

        if (invokeResult is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return (true, null);
        }

        if (invokeResult is not null && invokeResult.GetType().IsGenericType && invokeResult.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultProperty = invokeResult.GetType().GetProperty("Result");
            if (resultProperty is not null)
            {
                return (true, resultProperty.GetValue(invokeResult));
            }

            var asTask = invokeResult.GetType().GetMethod("AsTask", Type.EmptyTypes)?.Invoke(invokeResult, null) as Task;
            if (asTask is not null)
            {
                await asTask.ConfigureAwait(false);
                return (true, asTask.GetType().GetProperty("Result")?.GetValue(asTask));
            }
        }

        return (true, invokeResult);
    }

    /// <summary>
    /// Invokes a required method and throws when the method is not available on the current client API.
    /// </summary>
    /// <param name="target">Target instance containing the method.</param>
    /// <param name="methodName">Required method name.</param>
    /// <param name="args">Invocation arguments.</param>
    /// <exception cref="InvalidOperationException">Thrown when no compatible method is found.</exception>
    public static async Task InvokeRequiredAsync(object target, string methodName, params object?[] args)
    {
        var invoked = await TryInvokeAsync(target, methodName, args);
        if (!invoked)
        {
            throw new InvalidOperationException($"RabbitMQ method '{methodName}' is not available in the current client API.");
        }
    }

    /// <summary>
    /// Finds the first public instance method whose name and argument compatibility match the requested invocation.
    /// </summary>
    /// <param name="type">Target type to inspect.</param>
    /// <param name="methodName">Method name to locate.</param>
    /// <param name="args">Candidate invocation arguments used for compatibility checks.</param>
    /// <returns>The first compatible method if found; otherwise <see langword="null"/>.</returns>
    public static MethodInfo? FindCompatibleMethod(Type type, string methodName, object?[] args)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method =>
                string.Equals(method.Name, methodName, StringComparison.Ordinal)
                || method.Name.EndsWith($".{methodName}", StringComparison.Ordinal))
            .FirstOrDefault(method => TryResolveInvocableMethod(method, args, out _, out _));
    }

    private static bool TryResolveInvocableMethod(MethodInfo method, object?[] args, out MethodInfo invocableMethod, out object?[] invokeArgs)
    {
        invocableMethod = method;

        if (!method.IsGenericMethodDefinition)
        {
            return TryBuildInvocationArguments(method, args, out invokeArgs);
        }

        var genericParameters = method.GetGenericArguments();
        var inferredTypeArguments = new Type[genericParameters.Length];
        var inferred = new bool[genericParameters.Length];
        var parameters = method.GetParameters();

        if (args.Length > parameters.Length)
        {
            invokeArgs = Array.Empty<object?>();
            return false;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (!parameterType.IsGenericParameter)
            {
                continue;
            }

            var genericIndex = Array.IndexOf(genericParameters, parameterType);
            if (genericIndex < 0)
            {
                continue;
            }

            var arg = args[i];
            if (arg is null)
            {
                invokeArgs = Array.Empty<object?>();
                return false;
            }

            var argType = arg.GetType();
            if (inferred[genericIndex])
            {
                if (inferredTypeArguments[genericIndex] != argType)
                {
                    invokeArgs = Array.Empty<object?>();
                    return false;
                }

                continue;
            }

            inferredTypeArguments[genericIndex] = argType;
            inferred[genericIndex] = true;
        }

        if (inferred.Any(x => !x))
        {
            invokeArgs = Array.Empty<object?>();
            return false;
        }

        MethodInfo closedMethod;
        try
        {
            closedMethod = method.MakeGenericMethod(inferredTypeArguments);
        }
        catch
        {
            invokeArgs = Array.Empty<object?>();
            return false;
        }

        if (!TryBuildInvocationArguments(closedMethod, args, out invokeArgs))
        {
            return false;
        }

        invocableMethod = closedMethod;
        return true;
    }

    private static bool TryBuildInvocationArguments(MethodInfo method, object?[] providedArgs, out object?[] invokeArgs)
    {
        var parameters = method.GetParameters();
        invokeArgs = new object?[parameters.Length];

        if (providedArgs.Length > parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (i < providedArgs.Length)
            {
                var value = providedArgs[i];
                if (!TryConvertArgument(value, parameters[i].ParameterType, out var converted))
                {
                    return false;
                }

                invokeArgs[i] = converted;
                continue;
            }

            if (parameters[i].HasDefaultValue)
            {
                invokeArgs[i] = parameters[i].DefaultValue;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryConvertArgument(object? value, Type parameterType, out object? converted)
    {
        converted = null;

        var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (value is null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null)
            {
                return false;
            }

            return true;
        }

        var valueType = value.GetType();
        if (targetType.IsInstanceOfType(value) || targetType.IsAssignableFrom(valueType))
        {
            converted = value;
            return true;
        }

        if (targetType == typeof(ReadOnlyMemory<byte>))
        {
            if (value is byte[] bytes)
            {
                converted = new ReadOnlyMemory<byte>(bytes);
                return true;
            }

            if (value is Memory<byte> memory)
            {
                converted = (ReadOnlyMemory<byte>)memory;
                return true;
            }
        }

        if (targetType.IsEnum)
        {
            try
            {
                if (value is string enumName)
                {
                    converted = Enum.Parse(targetType, enumName, ignoreCase: true);
                    return true;
                }

                converted = Enum.ToObject(targetType, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            converted = Convert.ChangeType(value, targetType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes an object using async/sync dispose contracts or compatible close/dispose method names.
    /// </summary>
    /// <param name="obj">Object to dispose.</param>
    public static async Task DisposeAsync(object? obj)
    {
        if (obj is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (obj is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (obj is not null)
        {
            await TryInvokeAsync(obj, "CloseAsync");
            await TryInvokeAsync(obj, "Close");
            await TryInvokeAsync(obj, "DisposeAsync");
            await TryInvokeAsync(obj, "Dispose");
        }
    }
}
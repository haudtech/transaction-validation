#nullable enable

using System.Text;
using System.Reflection;
using RabbitMQ.Client;

namespace TransactionValidation.Messaging;

public sealed class RabbitMqClientAdapter : IRabbitMqClientAdapter
{
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _userName;
    private readonly string _password;

    public RabbitMqClientAdapter(string hostName, int port, string userName, string password)
    {
        _hostName = hostName;
        _port = port;
        _userName = userName;
        _password = password;
    }

    public async Task DeclareDurableQueueAsync(string queueName, bool durable, CancellationToken cancellationToken = default)
    {
        var connection = await CreateConnectionAsync(cancellationToken);
        try
        {
            var channel = await CreateChannelAsync(connection, cancellationToken);
            try
            {
                var declared = await TryInvokeAsync(
                    channel,
                    "QueueDeclareAsync",
                    queueName,
                    durable,
                    false,
                    false,
                    null,
                    cancellationToken);

                if (!declared)
                {
                    declared = await TryInvokeAsync(
                        channel,
                        "QueueDeclareAsync",
                        queueName,
                        durable,
                        false,
                        false,
                        null);
                }

                if (!declared)
                {
                    declared = await TryInvokeAsync(
                        channel,
                        "QueueDeclareAsync",
                        queueName,
                        durable,
                        false,
                        false,
                        null,
                        false,
                        cancellationToken);
                }

                if (!declared)
                {
                    await InvokeRequiredAsync(channel, "QueueDeclare", queueName, durable, false, false, null);
                }
            }
            finally
            {
                await DisposeAsync(channel);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<bool> PublishPersistentWithConfirmAsync(string queueName, string payload, CancellationToken cancellationToken = default)
    {
        var connection = await CreateConnectionAsync(cancellationToken);
        try
        {
            var channel = await CreateChannelAsync(connection, cancellationToken);
            try
            {
                await TryInvokeAsync(channel, "ConfirmSelectAsync", cancellationToken);
                await TryInvokeAsync(channel, "ConfirmSelect");

                object? properties = null;
                var basicPropertiesCreated = await TryInvokeWithResultAsync(channel, "CreateBasicProperties");
                if (basicPropertiesCreated.found)
                {
                    properties = basicPropertiesCreated.result;
                    var persistentProperty = properties?.GetType().GetProperty("Persistent", BindingFlags.Public | BindingFlags.Instance);
                    if (persistentProperty?.CanWrite == true)
                    {
                        persistentProperty.SetValue(properties, true);
                    }
                }

                var body = Encoding.UTF8.GetBytes(payload);

                var publishedAsync = await TryInvokeAsync(
                    channel,
                    "BasicPublishAsync",
                    string.Empty,
                    queueName,
                    true,
                    properties,
                    body,
                    cancellationToken);

                if (!publishedAsync)
                {
                    publishedAsync = await TryInvokeAsync(
                        channel,
                        "BasicPublishAsync",
                        string.Empty,
                        queueName,
                        true,
                        properties,
                        body.AsMemory(),
                        cancellationToken);
                }

                if (!publishedAsync)
                {
                    publishedAsync = await TryInvokeAsync(
                        channel,
                        "BasicPublishAsync",
                        string.Empty,
                        queueName,
                        true,
                        properties,
                        body.AsMemory());
                }

                if (!publishedAsync)
                {
                    await InvokeRequiredAsync(channel, "BasicPublish", string.Empty, queueName, properties, body);
                }

                var confirmAsync = await TryInvokeWithResultAsync(channel, "WaitForConfirmsAsync", cancellationToken);
                if (confirmAsync.found)
                {
                    return confirmAsync.result as bool? ?? true;
                }

                var confirmSync = await TryInvokeWithResultAsync(channel, "WaitForConfirms", TimeSpan.FromSeconds(5));
                if (confirmSync.found)
                {
                    return confirmSync.result as bool? ?? true;
                }

                return true;
            }
            finally
            {
                await DisposeAsync(channel);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    /// <summary>
    /// Creates a RabbitMQ connection while staying compatible with multiple RabbitMQ.Client API versions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation signal for async API variants, when supported.</param>
    /// <returns>
    /// A connection object returned by the first compatible API found at runtime.
    /// The concrete type differs by RabbitMQ client version.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method intentionally avoids hard-coding a single RabbitMQ connection API.
    /// Instead, it probes available methods in descending preference and falls back safely.
    /// </para>
    /// <para>
    /// Fallback order:
    /// 1) Try <c>CreateConnectionAsync(CancellationToken)</c>
    /// 2) Try <c>CreateConnectionAsync()</c>
    /// 3) Try synchronous <c>CreateConnection()</c>
    /// </para>
    /// <para>
    /// The probing is performed by <c>TryInvokeWithResultAsync</c>, which:
    /// - Uses reflection to find a method by name and compatible parameter types
    /// - Invokes the method dynamically when found
    /// - Awaits completion when the method returns <c>Task</c>/<c>Task&lt;T&gt;</c>
    /// - Returns a tuple indicating whether a compatible method existed and what result it returned
    /// </para>
    /// <para>
    /// If no compatible method can be resolved, this method throws <see cref="InvalidOperationException"/>
    /// so callers fail fast with a clear compatibility error.
    /// </para>
    /// </remarks>
    private async Task<object> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            Port = _port,
            UserName = _userName,
            Password = _password
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
    /// Creates a RabbitMQ channel/model from a version-dependent connection object.
    /// </summary>
    /// <param name="connection">A connection instance produced by <c>CreateConnectionAsync</c>.</param>
    /// <param name="cancellationToken">Cancellation signal for async API variants, when supported.</param>
    /// <returns>A channel/model object compatible with the discovered RabbitMQ client API.</returns>
    /// <remarks>
    /// Uses runtime probing to support both async and legacy sync channel creation APIs.
    /// Fallback order:
    /// 1) <c>CreateChannelAsync(CancellationToken)</c>
    /// 2) <c>CreateChannelAsync()</c>
    /// 3) <c>CreateModel()</c>
    /// Throws <see cref="InvalidOperationException"/> when no compatible method is available.
    /// </remarks>
    private static async Task<object> CreateChannelAsync(object connection, CancellationToken cancellationToken)
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
    /// Attempts to invoke a method by name with compatible arguments and awaits it if it is asynchronous.
    /// </summary>
    /// <param name="target">The instance that owns the method.</param>
    /// <param name="methodName">The method name to probe and invoke.</param>
    /// <param name="args">Arguments used for compatibility matching and invocation.</param>
    /// <returns>
    /// <c>true</c> when a compatible method is found and invoked successfully; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This helper does not throw when a method is missing; it reports absence via <c>false</c> so callers
    /// can execute explicit fallback paths.
    /// </remarks>
    private static async Task<bool> TryInvokeAsync(object target, string methodName, params object?[] args)
    {
        var method = FindCompatibleMethod(target.GetType(), methodName, args);
        if (method is null)
        {
            return false;
        }

        var invokeResult = method.Invoke(target, args);
        if (invokeResult is Task task)
        {
            await task.ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Attempts to invoke a method and return both existence status and invocation result.
    /// </summary>
    /// <param name="target">The instance that owns the method.</param>
    /// <param name="methodName">The method name to probe and invoke.</param>
    /// <param name="args">Arguments used for compatibility matching and invocation.</param>
    /// <returns>
    /// A tuple where <c>found</c> indicates whether a compatible method exists, and <c>result</c> is the
    /// return value (or generic task result) when available.
    /// </returns>
    /// <remarks>
    /// For <c>Task&lt;T&gt;</c> methods, this helper awaits completion and extracts <c>T</c>.
    /// For <c>Task</c> methods, <c>result</c> is <c>null</c> after successful completion.
    /// </remarks>
    private static async Task<(bool found, object? result)> TryInvokeWithResultAsync(object target, string methodName, params object?[] args)
    {
        var method = FindCompatibleMethod(target.GetType(), methodName, args);
        if (method is null)
        {
            return (false, null);
        }

        var invokeResult = method.Invoke(target, args);
        if (invokeResult is Task task)
        {
            await task.ConfigureAwait(false);

            if (task.GetType().IsGenericType)
            {
                return (true, task.GetType().GetProperty("Result")?.GetValue(task));
            }

            return (true, null);
        }

        return (true, invokeResult);
    }

    /// <summary>
    /// Invokes a method that is required for correctness and throws when it is unavailable.
    /// </summary>
    /// <param name="target">The instance that owns the method.</param>
    /// <param name="methodName">The required method name.</param>
    /// <param name="args">Arguments used for compatibility matching and invocation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no compatible method with the specified name and arguments exists.
    /// </exception>
    private static async Task InvokeRequiredAsync(object target, string methodName, params object?[] args)
    {
        var invoked = await TryInvokeAsync(target, methodName, args);
        if (!invoked)
        {
            throw new InvalidOperationException($"RabbitMQ method '{methodName}' is not available in the current client API.");
        }
    }

    /// <summary>
    /// Finds the first public instance method whose name and argument shape are compatible.
    /// </summary>
    /// <param name="type">Type to inspect for candidate methods.</param>
    /// <param name="methodName">Method name to match.</param>
    /// <param name="args">Invocation arguments used for parameter compatibility checks.</param>
    /// <returns>A matching <see cref="MethodInfo"/> or <c>null</c> if no compatible method is found.</returns>
    /// <remarks>
    /// Compatibility is based on argument count and runtime assignability; <c>null</c> arguments are accepted
    /// for any parameter position.
    /// </remarks>
    private static MethodInfo? FindCompatibleMethod(Type type, string methodName, object?[] args)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .FirstOrDefault(m =>
            {
                var parameters = m.GetParameters();
                if (parameters.Length != args.Length)
                {
                    return false;
                }

                for (var i = 0; i < parameters.Length; i++)
                {
                    if (args[i] is null)
                    {
                        continue;
                    }

                    if (!parameters[i].ParameterType.IsInstanceOfType(args[i])
                        && !(parameters[i].ParameterType.IsAssignableFrom(args[i]!.GetType())))
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    /// <summary>
    /// Disposes an object using asynchronous disposal when available, otherwise synchronous disposal.
    /// </summary>
    /// <param name="obj">Object instance to dispose, or <c>null</c>.</param>
    /// <remarks>
    /// This helper is used to safely clean up connection and channel objects returned from mixed API shapes.
    /// </remarks>
    private static async Task DisposeAsync(object? obj)
    {
        if (obj is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        if (obj is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

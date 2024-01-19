namespace IPPCPlugin;

using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using IPPCLibrary;
using System;
using System.Reflection;

public class IPPCPlugin : IDalamudPlugin
{
    public const string Name = "IPPC";

    [PluginService] public IPluginLog PluginLog { get; private set; } = null!;

    private static ServerClient? ServerClient;

    public IPPCPlugin(DalamudPluginInterface pluginInterface)
    {
        try
        {
            pluginInterface.Inject(this);

            ServerClient = new();
            ServerClient.LogMessage = (msg) => PluginLog.Information(msg);
            ServerClient.LogError = (e, msg) => PluginLog.Error(e, msg);
            ServerClient.OnInvoke = (name, args) => GetOrCreateChannel(name).Invoke(args);

            if (!ServerClient.StartServer())
            {
                PluginLog.Error($"Failed to start IPPC server on port {ServerClient.Port}");
            }
            else
            {
                PluginLog.Info($"Started IPPC server on port {ServerClient.Port}");
            }
        }
        catch(Exception)
        {
            ServerClient?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        ServerClient?.Dispose();
    }

    /// <summary>
    /// This entire function replicates the internal "Service<CallGate>.Get().GetOrCreateChannel(name);"
    /// </summary>
    private CallGateChannelStub GetOrCreateChannel(string name)
    {
        Assembly dalamudAssembly = typeof(DalamudPluginInterface).Assembly;

        // https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Service%7BT%7D.cs
        Type? callGateServiceType = dalamudAssembly.GetType("Dalamud.Service`1[Dalamud.Plugin.Ipc.Internal.CallGate]");
        if (callGateServiceType == null)
            throw new Exception("Failed to get ServiceManager type");

        MethodInfo? getMethod = callGateServiceType.GetMethod("Get");
        if (getMethod == null)
            throw new Exception("Failed to get CallGate Service Get method");

        // https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Ipc/Internal/CallGate.cs
        object? callGate = getMethod.Invoke(null, null);
        if (callGate == null)
            throw new Exception("Failed to get CallGate");

        Type callGateType = callGate.GetType();
        MethodInfo? getOrCreateChannelMethod = callGateType.GetMethod("GetOrCreateChannel");
        if (getOrCreateChannelMethod == null)
            throw new Exception("Failed to get GetOrCreateChannel method");

        // https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Ipc/Internal/CallGateChannel.cs
        object? callGateChannel = getOrCreateChannelMethod.Invoke(callGate, new[]{ name});
        if (callGateChannel == null)
            throw new Exception("Failed to get CallGateChannel");

        Type callGateChannelType = callGateChannel.GetType();
        PropertyInfo? funcProperty = callGateChannelType.GetProperty("Func");
        if (funcProperty == null)
            throw new Exception("Failed to get Func property");

        PropertyInfo? actionProperty = callGateChannelType.GetProperty("Action");
        if (actionProperty == null)
            throw new Exception("Failed to get Action property");

        Delegate? func = funcProperty.GetValue(callGateChannel) as Delegate;
        Delegate? action = actionProperty.GetValue(callGateChannel) as Delegate;

        CallGateChannelStub stub = default;
        stub.Func = func;
        stub.Action = action;
        return stub;
    }

    public struct CallGateChannelStub
    {
        public Delegate? Action { get; set; }
        public Delegate? Func { get; set; }

        public object? Invoke(params object[] p)
        {
            if (this.Func != null)
            {
                return this.Func.DynamicInvoke(p);
            }

            if (this.Action != null)
            {
                return this.Action.DynamicInvoke(p);
            }

            return null;
        }
    }
}
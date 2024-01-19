namespace IPPCLibrary;

using EasyTcp4;
using EasyTcp4.ClientUtils;
using EasyTcp4.ClientUtils.Async;
using EasyTcp4.ServerUtils;
using IPPCLibrary.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public class ServerClient
{
    public const int Port = 1200;

    private const string responseName = "IPPCResponse";

    private EasyTcpServer? server;
    private EasyTcpClient? client;
    private readonly Dictionary<string, IpcMessage> responses = new();

    public Action<Exception, string>? LogError;
    public Action<string>? LogMessage;
    public Func<string, object[], object?>? OnInvoke;

    public bool IsAlive
    {
        get
        {
            if (this.server != null)
                return this.server.IsRunning;

            if (this.client != null)
                return this.client.IsConnected();

            return false;
        }
    }

    public bool StartServer()
    {
        if (this.client != null || this.server != null)
            throw new Exception("Attempt to start IPC server while it is already running");

        this.server = new();
        this.server.EnableServerKeepAlive();
        this.server.OnDataReceiveAsync += OnDataReceived;
        this.server.OnError += (s, e) => this.LogError?.Invoke(e, "IPC error");
        this.server.OnConnect += (s, e) => this.LogMessage?.Invoke("IPC client connected");
        this.server.OnDisconnect += (s, e) => this.LogMessage?.Invoke("IPC client disconnected");

        this.server.Start(Port);

        return this.server.IsRunning;
    }

    public async Task<bool> StartClient()
    {
        if (this.client != null || this.server != null)
            throw new Exception("Attempt to start IPC client while it is already running");

        this.client = new();
        this.client.OnError += (s, e) => LogError?.Invoke(e, "IPC error");
        this.client.OnDataReceiveAsync += this.OnDataReceived;

        return await this.client.ConnectAsync(IPAddress.Loopback, Port);
    }

    public void Dispose()
    {
        this.server?.Dispose();
        this.client?.Dispose();
    }

    /// <summary>
    /// Invoke an IPC method and wait for it to complete.
    /// </summary>
    public Task Invoke(string name, params object[] param)
    {
        IpcMessage request = new(name);
        request.SetParameters(param);
        return this.SendRequest(request);
    }

    /// <summary>
    /// Invoke an IPC method and get its return value.
    /// </summary>
    public async Task<T> Invoke<T>(string name, params object[] param)
        where T : notnull
    {
        IpcMessage request = new(name);
        request.SetParameters(param);
        return await this.SendRequest<T>(request);
    }

    private async Task<T> SendRequest<T>(IpcMessage msg)
        where T : notnull
    {
        IpcMessage response = await this.SendRequest(msg);

        object? obj = response.Result?.ToValue();

        if (obj is T tObj)
            return tObj;

        if (obj is string sObj)
        {
            return Serializer.Deserialize<T>(sObj);
        }

        throw new Exception($"IPC response result: {obj} was not type: {typeof(T)}");
    }

    private async Task<IpcMessage> SendRequest(IpcMessage msg)
    {
        string json = Serializer.Serialize(msg);
        this.client.Send(json);

        while (!this.responses.ContainsKey(msg.Id))
            await Task.Delay(50);

        IpcMessage response = this.responses[msg.Id];
        this.responses.Remove(msg.Id);
        return response;
    }

    private async Task OnDataReceived(object? sender, Message e)
    {
        string json = Encoding.UTF8.GetString(e.Data);
        IpcMessage? msg = Serializer.Deserialize<IpcMessage>(json);

        if (msg == null)
            return;

        if (msg.Name == responseName)
        {
            this.responses.Add(msg.Id, msg);
        }
        else
        {
            await this.HandleRequest(msg);
        }
    }

    private async Task HandleRequest(IpcMessage msg)
    {
        IpcMessage response = new(responseName);
        response.Id = msg.Id;

        if (this.OnInvoke != null && msg.Name != null)
        {
            object[] param = msg.GetParameters();
            object? returnValue = this.OnInvoke.Invoke(msg.Name, param);

            // If this method returned a task, await it and get its response.
            if (returnValue is Task task)
            {
                await task;

                PropertyInfo? resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                if (resultProperty != null)
                {
                    returnValue = resultProperty.GetValue(task);
                }
                else
                {
                    returnValue = null;
                }
            }

            // Add the method return value to the response
            if (returnValue != null)
            {
                response.Result = IpcMessage.MessageParameter.FromObject(returnValue);
            }
        }

        string responseJson = Serializer.Serialize(response);
        this.server.SendAll(responseJson);
    }
}

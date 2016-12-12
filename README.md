MagicOnion
===
gRPC based HTTP2 RPC Streaming Framework for .NET, .NET Core and Unity.

Work in progress, stay tuned.

Quick Start
---
alpha-version is available in NuGet(Currently only work on .NET 4.5, .NET Core and Unity is not yet)

* Install-Package MagicOnion -Pre

Let's implements Server, Server has two parts, interface and implementation.

```csharp
using Grpc.Core;
using MagicOnion;
using MagicOnion.Server;
using System;

// define interface as Server/Client IDL.
// implements T : IService<T>.
public interface IMyFirstService : IService<IMyFirstService>
{
    UnaryResult<int> SumAsync(int x, int y);
}

// implement RPC service.
// inehrit ServiceBase<interface>, interface
public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService
{
    public UnaryResult<int> SumAsync(int x, int y)
    {
        Logger.Debug($"Received:{x}, {y}");

        return UnaryResult(x + y);
    }
}
```

and, launch the server.

```csharp
class Program
{
    static void Main(string[] args)
    {
        GrpcEnvironment.SetLogger(new Grpc.Core.Logging.ConsoleLogger());

        var service = MagicOnionEngine.BuildServerServiceDefinition(isReturnExceptionStackTraceInErrorDetail: true);

        var server = new global::Grpc.Core.Server
        {
            Services = { service },
            Ports = { new ServerPort("localhost", 12345, ServerCredentials.Insecure) }
        };
        
        // launch gRPC Server.
        server.Start();

        // sample, launch server/client in same app.
        Task.Run(() =>
        {
            ClientImpl();
        });

        Console.ReadLine();
    }

    // Blank, used by next section 
    static async void ClientImpl()
    {
    }
}
```

write the client.

```csharp
static async void ClientImpl()
{
    // standard gRPC channel
    var channel = new Channel("localhost", 12345, ChannelCredentials.Insecure);

    // create MagicOnion dynamic client proxy
    var client = MagicOnionClient.Create<IMyFirstService>(channel);

    // call method.
    var result = await client.SumAsync(100, 200);
    Console.WriteLine("Client Received:" + result);
}
```

MagicOnion allows primitive, multiple request value. Complex type is serialized by [ZeroFormatter](https://github.com/neuecc/ZeroFormatter) so type should follow ZeroFormatter rules. 

Project Structure
---
If creates Server-Client project, I recommend make three projects. `Server`, `ServerDefinition`, `Client`.

![image](https://cloud.githubusercontent.com/assets/46207/21081857/e0f6dfce-c012-11e6-850d-358c5b928a82.png)

ServerDefinition is only defined interface(`IService<T>`)(and some share request/response types).

If debugging, I recommend use [SwitchStartupProject](https://marketplace.visualstudio.com/items?itemName=vs-publisher-141975.SwitchStartupProject) and launch both Server and Client.

```json
"MultiProjectConfigurations": {
    "Server + Client": {
        "Projects": {
            "FooService": {},
            "FooClient": {}
        }
    }
}
```

It can step-in/out seamlessly in server and client.

Streaming
---


```csharp
// Definitions
public interface IMyFirstService : IService<IMyFirstService>
{
    Task<UnaryResult<string>> SumAsync(int x, int y);
    Task<ClientStreamingResult<int, string>> ClientStreamingSampleAsync();
    Task<ServerStreamingResult<string>> ServertSreamingSampleAsync(int x, int y, int z);
    Task<DuplexStreamingResult<int, string>> DuplexStreamingSampleAync();
}

// Server
public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService
{
    public async Task<UnaryResult<string>> SumAsync(int x, int y)
    {
        Logger.Debug($"Called SumAsync - x:{x} y:{y}");

        // If Unary, return UnaryResult value.
        return UnaryResult((x + y).ToString());
    }

    public async Task<ClientStreamingResult<int, string>> ClientStreamingSampleAsync()
    {
        Logger.Debug($"Called ClientStreamingSampleAsync");

        // If ClientStreaming, use GetClientStreamingContext.
        var stream = GetClientStreamingContext<int, string>();

        // receive from client asynchronously
        await stream.ForEachAsync(x =>
        {
            Logger.Debug("Client Stream Received:" + x);
        });

        // StreamingContext.Result() for result value.
        return stream.Result("finished");
    }

    public async Task<ServerStreamingResult<string>> ServertSreamingSampleAsync(int x, int y, int z)
    {
        Logger.Debug($"Called ServertSreamingSampleAsync - x:{x} y:{y} z:{z}");

        var stream = GetServerStreamingContext<string>();

        var acc = 0;
        for (int i = 0; i < z; i++)
        {
            acc = acc + x + y;
            await stream.WriteAsync(acc.ToString());
        }

        return stream.Result();
    }

    public async Task<DuplexStreamingResult<int, string>> DuplexStreamingSampleAync()
    {
        Logger.Debug($"Called DuplexStreamingSampleAync");

        // DuplexStreamingContext represents both server and client streaming.
        var stream = GetDuplexStreamingContext<int, string>();

        var waitTask = Task.Run(async () =>
        {
            // ForEachAsync(MoveNext, Current) can receive client streaming.
            await stream.ForEachAsync(x =>
            {
                Logger.Debug($"Duplex Streaming Received:" + x);
            });
        });

        // WriteAsync is ServerStreaming.
        await stream.WriteAsync("test1");
        await stream.WriteAsync("test2");
        await stream.WriteAsync("finish");

        await waitTask;

        return stream.Result();
    }
}
```

Client sample.

```csharp
static async Task UnaryRun(IMyFirstService client)
{
    // await await
    var vvvvv = await await client.SumAsync(10, 20);
    Console.WriteLine("SumAsync:" + vvvvv);
}

static async Task ClientStreamRun(IMyFirstService client)
{
    var stream = await client.ClientStreamingSampleAsync();

    for (int i = 0; i < 3; i++)
    {
        await stream.RequestStream.WriteAsync(i);
    }
    await stream.RequestStream.CompleteAsync();

    var response = await stream.ResponseAsync;

    Console.WriteLine("Response:" + response);
}

static async Task ServerStreamRun(IMyFirstService client)
{
    var stream = await client.ServertSreamingSampleAsync(10, 20, 3);

    await stream.ResponseStream.ForEachAsync(x =>
    {
        Console.WriteLine("ServerStream Response:" + x);
    });
}

static async Task DuplexStreamRun(IMyFirstService client)
{
    var stream = await client.DuplexStreamingSampleAync();

    var count = 0;
    await stream.ResponseStream.ForEachAsync(async x =>
    {
        Console.WriteLine("DuplexStream Response:" + x);

        await stream.RequestStream.WriteAsync(count++);
        if (x == "finish")
        {
            await stream.RequestStream.CompleteAsync();
        }
    });
}
```
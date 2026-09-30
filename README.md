# AssettoServer [![Build status](https://img.shields.io/github/actions/workflow/status/compujuckel/AssettoServer/dotnet.yml?logo=github&label=Build)](https://github.com/compujuckel/AssettoServer/actions/workflows/dotnet.yml) [![Discord](https://img.shields.io/discord/890676433746268231?logo=discord&label=Discord&color=7289da)](https://discord.gg/uXEXRcSkyz) ![GitHub Downloads)](https://img.shields.io/github/downloads/compujuckel/AssettoServer/total?color=gold&label=Downloads) [![Docker Hub](https://img.shields.io/docker/v/compujuckel/assettoserver?logo=docker&label=Docker)](https://hub.docker.com/r/compujuckel/assettoserver)

## About
AssettoServer is a custom game server for Assetto Corsa developed with freeroam in mind. It greatly improves upon the default game server by fixing various security issues and providing new features like AI traffic and dynamic weather.

Race and Qualification sessions are in an experimental state and might need more testing.

This is a fork of https://github.com/Niewiarowski/AssettoServer.

## Documentation
For more information on configuration, admin commands, etc. also check out our [website](https://assettoserver.org/).

### Plugin dependency injection migration
Plugins now use `Microsoft.Extensions.DependencyInjection`. DLL plugins, configuration, controllers, and constructor-injected services remain supported. Plugins overriding Autofac's `Load(ContainerBuilder)` must migrate and rebuild; there is no binary compatibility layer.

Override `ConfigureServices(IServiceCollection)` to register plugin services:

```csharp
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace SamplePlugin;

public class SampleModule : AssettoServerModule<SampleConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<Sample>();
    }
}
```

Use `AddSingleton<T>()` or `AddSingleton<IService, Implementation>()` for former `SingleInstance()` registrations. Use `AddTransient<T>()` (or its interface overload) for former default registrations without runtime arguments. `AddSingletonHostedService<T>()` shares one singleton between self and `IHostedService`, preserving startup order. `services.AddAutoActivatedSingleton<StartupListener>();` eagerly activates before server startup without registering a hosted service.

For runtime `EntryCar` or `ACTcpClient` arguments, register a factory instead of `AddTransient<T>()`: `services.AddTransientFactory<Func<EntryCar, PerCarService>>();` (`EntryCar` is in `AssettoServer.Server`). Other constructor dependencies come from DI. `Func` arguments match by type; custom delegates such as `services.AddTransientFactory<Race.Factory>();` match by parameter name. Disposable products belong to the resolving DI scope. These helpers are public extensions in `AssettoServer.Server.Plugin`.

## Getting help
If you have trouble setting up a server feel free to visit the #server-troubleshooting channel on our [Discord](https://discord.gg/uXEXRcSkyz).

**Please don't use the Issue tracker for installation help or configuration questions. Also make sure to read the documentation first before asking questions that are already answered there!**

## License
AssettoServer is licensed under the GNU Affero General Public License v3.0, see [LICENSE](https://github.com/compujuckel/AssettoServer/blob/master/LICENSE) for more info.  
Additionally, you must preserve the legal notices and author attributions present in the server.

```
Copyright (C)  2025 Niewiarowski, compujuckel

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.


Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it 
with the Steamworks SDK by Valve Corporation, containing parts covered by the
terms of the Steamworks SDK License, the licensors of this Program grant you
additional permission to convey the resulting work.

Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it 
with plugins published on https://www.patreon.com/assettoserver, the licensors
of this Program grant you additional permission to convey the resulting work.
```

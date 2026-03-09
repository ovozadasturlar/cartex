import { dotnet } from './_framework/dotnet.js';

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArguments()
    .create();

const config = dotnetRuntime.getConfig();
await dotnetRuntime.runMainAndExit(config.mainAssemblyName, [window.location.search]);

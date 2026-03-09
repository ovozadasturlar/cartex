import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error('Expected to be running in a browser');

try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (e) {
    const splash = document.getElementById('splash');
    if (splash) splash.textContent = 'Error: ' + e.message;
    console.error('WASM init failed:', e);
}

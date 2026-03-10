import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error('Expected to be running in a browser');

const splash = document.getElementById('splash');

try {
    if (splash) splash.textContent = 'Initializing runtime...';

    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    if (splash) splash.textContent = 'Starting application...';

    const config = dotnetRuntime.getConfig();
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (e) {
    if (splash) {
        splash.style.color = '#EF4444';
        splash.textContent = 'Error: ' + (e.message || e);
    }
    console.error('WASM init failed:', e);
}

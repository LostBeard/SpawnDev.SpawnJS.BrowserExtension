# SpawnDev.SpawnJS.BrowserExtension

[![NuGet](https://img.shields.io/nuget/dt/SpawnDev.SpawnJS.BrowserExtension.svg?label=SpawnDev.SpawnJS.BrowserExtension)](https://www.nuget.org/packages/SpawnDev.SpawnJS.BrowserExtension)

SpawnDev.SpawnJS.BrowserExtension adds the ability for .Net WASM to run as a web browser Manifest V3 extension. .Net can run in all extension contexts. Runs in content context (in page with any site), popup window, options window, and the background.

## Features
- Manifest V3
- Create a fully functional Manifest V3 web browser extension without writing a single line of Javascript
- Multi-platform extension builds (Firefox, Chrome, etc)
- Shared and platform specific manifest entries
- .Net WASM runs in ALL extension contexts: 
  - Background page (Firefox)
  - Background ServiceWorker (Chrome)
  - Content script
  - Options, Popup, etc
- Background suspend/resume compatible for instant resume and power efficiency
- Direct access to extension [APIs](https://developer.chrome.com/docs/extensions/reference/api) via C#
  - Extension, Runtime, Tabs, Windows, Storage, etc

### Dependencies
- [SpawnDev.SpawnJS](https://github.com/LostBeard/SpawnDev.SpawnJS) - Enables full access to the Javascript environment, and Javascript class wrapping.
- [SpawnDev.SpawnJS.WebWorkers](https://github.com/LostBeard/SpawnDev.SpawnJS.WebWorkers) - Enables running Blazor WebAssembly in any web browser context, and inter-context communication.


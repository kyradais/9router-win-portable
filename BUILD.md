# Building 9Router Portable

## Prerequisites (Build Machine Only)
- .NET 8 SDK
- Node.js & npm (for building upstream 9Router)
- PowerShell 7+ or Windows PowerShell

## Build Steps
Run the automated build script:
```powershell
pwsh scripts/build-portable.ps1
```

The output will be generated under `dist/9RouterPortable/` and packaged as `dist/9RouterPortable-win-x64.zip`.

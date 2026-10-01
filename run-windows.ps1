$ErrorActionPreference = 'Stop'
$appProject = Join-Path $PSScriptRoot 'PausaActiva/PausaActiva.csproj'
dotnet build $appProject -f net10.0-windows10.0.19041.0 -p:MauiTargetFramework=net10.0-windows10.0.19041.0
if ($LASTEXITCODE -ne 0) { throw 'Ritmia could not be built.' }
$appExecutable = Join-Path $PSScriptRoot 'PausaActiva/bin/Debug/net10.0-windows10.0.19041.0/win-x64/Ritmia.exe'
Start-Process -FilePath $appExecutable

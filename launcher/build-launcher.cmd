@echo off
setlocal
set "ROOT=%~dp0.."
set "PROJECT=%~dp0Fable2.Launcher\Fable2.Launcher.csproj"
set "OUTPUT=%ROOT%\out\launcher"

if /i "%~1"=="self-contained" (
  dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%OUTPUT%"
) else (
  dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false ^
    -p:PublishSingleFile=true -o "%OUTPUT%"
)

if errorlevel 1 exit /b 1
echo Launcher built: %OUTPUT%\Fable2Launcher.exe

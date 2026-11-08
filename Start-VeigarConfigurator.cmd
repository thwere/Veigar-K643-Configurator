@echo off
setlocal
set "NET48_RELEASE="
for /f "tokens=3" %%A in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2^>nul ^| findstr /I "Release"') do set "NET48_RELEASE=%%A"
if not defined NET48_RELEASE goto MissingRuntime
set /a NET48_RELEASE=%NET48_RELEASE%
if %NET48_RELEASE% LSS 528040 goto MissingRuntime
if not exist "%~dp0VeigarConfigurator.exe" (
    echo VeigarConfigurator.exe was not found.
    echo Extract the complete ZIP archive before starting the program.
    pause
    exit /b 1
)
echo Starting Veigar K643 Configurator...
"%~dp0VeigarConfigurator.exe"
exit /b %ERRORLEVEL%

:MissingRuntime
echo Microsoft .NET Framework 4.8 or newer is required.
echo Install it from:
echo https://dotnet.microsoft.com/download/dotnet-framework/net48
echo Then restart this program.
pause
exit /b 1

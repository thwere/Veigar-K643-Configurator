@echo off
setlocal

set "MSBUILD="

for /f "delims=" %%M in ('where msbuild.exe 2^>nul') do (
    if not defined MSBUILD set "MSBUILD=%%M"
)

if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%M in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        if not defined MSBUILD set "MSBUILD=%%M"
    )
)

if not defined MSBUILD if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
)

if not defined MSBUILD (
    echo MSBuild was not found.
    echo Install Visual Studio 2022 or Visual Studio Build Tools with:
    echo - .NET desktop development workload
    echo - .NET Framework 4.8 targeting pack
    exit /b 1
)

echo Using: "%MSBUILD%"
"%MSBUILD%" "%~dp0VeigarConfigurator.sln" /m /p:Configuration=Release /verbosity:minimal

if errorlevel 1 (
    echo.
    echo Build failed. Check the compiler output above.
    exit /b 1
)

echo.
echo Build succeeded.
echo Output: src\VeigarConfigurator.App\bin\Release\VeigarConfigurator.exe
exit /b 0

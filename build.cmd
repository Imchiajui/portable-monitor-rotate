@echo off
setlocal
rem Build MonitorRotateTray.exe using the C# compiler bundled with Windows.
rem No .NET SDK, no NuGet, no project file required.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: could not find csc.exe ^(.NET Framework 4.x^).
    exit /b 1
)

if not exist "%~dp0bin" mkdir "%~dp0bin"

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
    /out:"%~dp0bin\MonitorRotateTray.exe" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll ^
    "%~dp0src\MonitorRotateTray.cs"

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Built: %~dp0bin\MonitorRotateTray.exe
endlocal

@echo off
setlocal
cd /d "%~dp0"
echo Restoring NuGet packages...
dotnet restore BlatmetroBrowser.csproj -r win-x64
if errorlevel 1 goto :error
echo Publishing self-contained Blatmetro Browser for Windows x64...
dotnet publish BlatmetroBrowser.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
if errorlevel 1 goto :error
echo.
echo Publish completed. Copy the entire publish\win-x64 folder to the target PC.
pause
exit /b 0
:error
echo.
echo Build failed. See the error messages above.
pause
exit /b 1

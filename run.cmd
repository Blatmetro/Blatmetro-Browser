@echo off
setlocal
cd /d "%~dp0"
echo Restoring NuGet packages...
dotnet restore BlatmetroBrowser.csproj -r win-x64
if errorlevel 1 goto :error
echo Starting Blatmetro Browser...
dotnet run --project BlatmetroBrowser.csproj -c Release -r win-x64 --no-restore
if errorlevel 1 goto :error
exit /b 0
:error
echo.
echo Browser failed to start. Read the error messages above.
pause
exit /b 1

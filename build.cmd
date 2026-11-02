@echo off
setlocal
cd /d "%~dp0"
echo Restoring NuGet packages...
dotnet restore BlatmetroBrowser.csproj -r win-x64
if errorlevel 1 goto :error
echo Building Blatmetro Browser...
dotnet build BlatmetroBrowser.csproj -c Release -r win-x64 --no-restore
if errorlevel 1 goto :error
echo.
echo Build completed. Output: bin\Release\net10.0-windows\win-x64\
pause
exit /b 0
:error
echo.
echo Build failed. See the error messages above.
pause
exit /b 1

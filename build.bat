@echo off
setlocal
cd /d "%~dp0"

set "VERSION=0.6.5.0"

echo Building Ceprkac v%VERSION%...
echo.

echo Step 1: Publishing application...
dotnet publish Ceprkac.csproj -c Release -r win-x64 --self-contained true -o bin\publish
if errorlevel 1 goto error

echo.
echo Step 2: Copying icon...
copy /Y Ceprkac.ico bin\publish\

echo.
echo Step 3: Building installer...
REM Inno Setup outputs to releases\%VERSION%\ (defined in Ceprkac.iss OutputDir)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Ceprkac.iss
if errorlevel 1 goto error

echo.
echo Build complete!
echo Output: releases\%VERSION%\Ceprkac-%VERSION%-Setup.exe
goto end

:error
echo.
echo BUILD FAILED!
exit /b 1

:end

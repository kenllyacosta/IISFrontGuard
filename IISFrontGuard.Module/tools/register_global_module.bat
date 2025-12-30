@echo off
setlocal EnableDelayedExpansion

REM -------------------------------------------------------------------------
REM register_global_module.bat
REM Automates global registration of a managed IHttpModule in IIS.
REM Usage:
REM   register_global_module.bat "C:\path\to\IISFrontGuard.Module.dll" [Version] [PublicKeyToken]
REM If Version or PublicKeyToken are omitted, the script will attempt to read them
REM from the assembly using PowerShell.
REM -------------------------------------------------------------------------

REM ----------- Configuration (can be overridden via args) ---------------
set "DLL_PATH=%~1"
if "%DLL_PATH%"=="" set "DLL_PATH=C:\Program Files\IISFrontGuard\IISFrontGuard.Module.dll"

set "ASSEMBLY_NAME=IISFrontGuard.Module"
set "MODULE_CLASS=IISFrontGuard.Module.FrontGuardModule"
set "MODULE_NAME=FrontGuardModule"
set "VERSION=%~2"
set "CULTURE=neutral"
set "PUBLIC_KEY_TOKEN=%~3"

REM ----------------- Require Administrator --------------------------------
net session >nul 2>&1
if %errorlevel% NEQ 0 (
  echo ERROR: Administrative privileges required. Right-click and Run as Administrator.
  pause
  exit /b 1
)

REM ----------------- Validate DLL ---------------------------------------
if not exist "%DLL_PATH%" (
  echo ERROR: DLL not found: "%DLL_PATH%"
  exit /b 1
)

REM ----------------- Discover Version/PublicKeyToken if needed ----------
if "%VERSION%"=="" (
  for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "[System.Reflection.AssemblyName]::GetAssemblyName('%DLL_PATH%').Version.ToString()"`) do set "VERSION=%%v"
)

if "%PUBLIC_KEY_TOKEN%"=="" (
  for /f "usebackq delims=" %%t in (`powershell -NoProfile -Command "$t=[System.Reflection.AssemblyName]::GetAssemblyName('%DLL_PATH%').GetPublicKeyToken(); if($t){ [System.BitConverter]::ToString($t) -replace '-','' | ForEach-Object { $_.ToLower() } }"`) do set "PUBLIC_KEY_TOKEN=%%t"
)

if "%PUBLIC_KEY_TOKEN%"=="" (
  echo WARNING: Could not determine PublicKeyToken automatically. Provide it as 3rd arg to this script.
)

echo.
echo Using:
echo   DLL_PATH=%DLL_PATH%
echo   ASSEMBLY_NAME=%ASSEMBLY_NAME%
echo   MODULE_CLASS=%MODULE_CLASS%
echo   MODULE_NAME=%MODULE_NAME%
echo   VERSION=%VERSION%
echo   CULTURE=%CULTURE%
echo   PUBLIC_KEY_TOKEN=%PUBLIC_KEY_TOKEN%
echo.

REM ----------------- Backup applicationHost.config -----------------------
set "AHC=%windir%\System32\inetsrv\config\applicationHost.config"
if exist "%AHC%" (
  for /f "usebackq delims=" %%T in (`powershell -NoProfile -Command "(Get-Date).ToString('yyyyMMddHHmmss')"`) do set TIMESTAMP=%%T
  set "BACKUP=%~dp0applicationHost.config.backup_%TIMESTAMP%"
  echo Backing up %AHC% to %BACKUP%
  powershell -NoProfile -Command "Copy-Item -Path '%AHC%' -Destination '%BACKUP%' -Force" >nul 2>&1
  if errorlevel 1 (
    echo ERROR: Could not back up applicationHost.config. Aborting.
    exit /b 1
  )
) else (
  echo ERROR: applicationHost.config not found at %AHC%
  exit /b 1
)

REM ----------------- appcmd path check ----------------------------------
set "APPCMD=%windir%\System32\inetsrv\appcmd.exe"
if not exist "%APPCMD%" (
  echo ERROR: __appcmd__ not found at %APPCMD%. Ensure IIS is installed.
  exit /b 1
)

REM ----------------- Remove existing module (safe, idempotent) ----------
echo Removing existing server-level module entry (if present)...
"%APPCMD%" set config /section:system.webServer/modules /-"[name='%MODULE_NAME%']" >nul 2>&1

REM ----------------- Register managed module globally -------------------
set "TYPE=%MODULE_CLASS%, %ASSEMBLY_NAME%, Version=%VERSION%, Culture=%CULTURE%, PublicKeyToken=%PUBLIC_KEY_TOKEN%"

echo Registering managed module at server level using __appcmd__...
"%APPCMD%" set config /section:system.webServer/modules /+"[name='%MODULE_NAME%',type='%TYPE%',preCondition='managedHandler,runtimeVersionv4.0']"
if errorlevel 1 (
  echo ERROR: Failed to add module via __appcmd__. See output above.
  exit /b 1
)

REM ----------------- Restart IIS to apply changes ------------------------
echo Restarting IIS service (__iisreset__)...
iisreset >nul 2>&1

REM ----------------- Verify registration --------------------------------
echo.
echo Verifying module registration...
"%APPCMD%" list modules | findstr /i "%MODULE_NAME%" >nul 2>&1
if %errorlevel% EQU 0 (
  echo SUCCESS: Module '%MODULE_NAME%' registered server-wide.
  echo Verify in IIS Manager -> Modules (server) and test a request.
) else (
  echo WARNING: Module '%MODULE_NAME%' not found in appcmd list. Manual verification recommended.
)

echo.
echo Done.
endlocal
exit /b 0
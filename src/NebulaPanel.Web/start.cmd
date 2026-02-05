@echo off
:: Nebula Panel - Process Wrapper Script (Windows)
:: Handles updates and automatic restarts
::
:: Exit codes:
::   0   = Clean shutdown (no restart)
::   100 = Update requested (run updater, then restart)
::   *   = Crash (restart after delay)

setlocal enabledelayedexpansion

:: Get the directory where this script is located
set "APP_DIR=%~dp0"
:: Remove trailing backslash
set "APP_DIR=%APP_DIR:~0,-1%"

set "MAIN_APP=%APP_DIR%\NebulaPanel.Web.exe"
set "UPDATER=%APP_DIR%\NebulaPanel.Updater.exe"
set "LOG_DIR=%APP_DIR%\data\logs"
set "LOG_FILE=%LOG_DIR%\wrapper.log"

:: Ensure log directory exists
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

call :log "=========================================="
call :log "Nebula Panel Wrapper Starting"
call :log "App Directory: %APP_DIR%"
call :log "=========================================="

:loop
call :log "Launching main application..."

:: Run the main application
if exist "%MAIN_APP%" (
    "%MAIN_APP%"
) else if exist "%APP_DIR%\NebulaPanel.Web.dll" (
    dotnet "%APP_DIR%\NebulaPanel.Web.dll"
) else (
    call :log "ERROR: Could not find NebulaPanel.Web executable or DLL"
    exit /b 1
)

set "EXIT_CODE=%ERRORLEVEL%"
call :log "Application exited with code %EXIT_CODE%"

if %EXIT_CODE% equ 100 (
    :: Update requested
    call :log "Update requested, running updater..."

    if exist "%UPDATER%" (
        "%UPDATER%" "%APP_DIR%"
    ) else if exist "%APP_DIR%\NebulaPanel.Updater.dll" (
        dotnet "%APP_DIR%\NebulaPanel.Updater.dll" "%APP_DIR%"
    ) else (
        call :log "ERROR: Could not find NebulaPanel.Updater executable or DLL"
        call :log "Skipping update, restarting application..."
    )

    set "UPDATER_EXIT=!ERRORLEVEL!"

    if !UPDATER_EXIT! neq 0 (
        call :log "Updater failed with code !UPDATER_EXIT!, restarting in 5s..."
        timeout /t 5 /nobreak >nul
    ) else (
        call :log "Update completed successfully"
    )

    goto loop

) else if %EXIT_CODE% equ 0 (
    :: Clean exit
    call :log "Application exited normally (clean shutdown)"
    call :log "Wrapper exiting"
    exit /b 0

) else (
    :: Crash - restart after delay
    call :log "Application crashed (code %EXIT_CODE%), restarting in 5s..."
    timeout /t 5 /nobreak >nul
    goto loop
)

:log
:: Log to console and file with timestamp
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set "DT=%%I"
set "TIMESTAMP=%DT:~0,4%-%DT:~4,2%-%DT:~6,2% %DT:~8,2%:%DT:~10,2%:%DT:~12,2%"
echo [%TIMESTAMP%] %~1
echo [%TIMESTAMP%] %~1 >> "%LOG_FILE%"
exit /b 0

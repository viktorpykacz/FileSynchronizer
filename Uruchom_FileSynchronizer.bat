@echo off
title FileSynchronizer
cd /d "%~dp0"
dotnet run --project src\FileSynchronizer.UI
if %errorlevel% neq 0 (
    echo.
    echo Wystapil blad podczas uruchamiania. Nacisnij dowolny klawisz, aby zamknac...
    pause >nul
)

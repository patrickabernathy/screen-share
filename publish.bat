@echo off
rem Builds publish\ScreenShare.exe. Exit Screen Share first, or the running exe can't be replaced.
cd /d "%~dp0"
dotnet publish src\ScreenShare -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
if errorlevel 1 pause

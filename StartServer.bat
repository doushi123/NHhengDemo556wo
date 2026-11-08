@echo off
cd /d "%~dp0"
dotnet run --project "Server\SecretCore.Server\SecretCore.Server.csproj"
pause

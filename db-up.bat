@echo off
REM Sobe apenas o banco de dados (Postgres) do Nexora.
REM Requer Docker Desktop em execucao e um arquivo .env com POSTGRES_PASSWORD.

setlocal
cd /d "%~dp0"

docker compose up -d --wait postgres
if errorlevel 1 (
    echo.
    echo Falha ao subir o Postgres. Verifique os logs: docker compose logs postgres
    exit /b 1
)

echo.
echo Postgres pronto em localhost:5432
endlocal

@echo off
REM Sobe o banco COMPARTILHADO saas-postgres (PostgreSQL 18, database saas_dev).
REM O container e definido em ..\infra\docker-compose.yml, nao neste repo.
REM Requer Docker Desktop em execucao.

setlocal
cd /d "%~dp0..\infra"

if not exist docker-compose.yml (
    echo.
    echo Nao encontrei ..\infra\docker-compose.yml. Ajuste o caminho do repo de infra.
    exit /b 1
)

docker compose up -d --wait
if errorlevel 1 (
    echo.
    echo Falha ao subir o saas-postgres. Verifique os logs: docker compose logs postgres
    exit /b 1
)

echo.
echo saas-postgres pronto em localhost:5432 (database saas_dev, schema nexora)
endlocal

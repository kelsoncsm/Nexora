@echo off
REM Para o container do banco de dados (Postgres) do Nexora. Mantem os dados no volume.

setlocal
cd /d "%~dp0"

docker compose stop postgres
endlocal

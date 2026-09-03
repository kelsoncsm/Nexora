@echo off
REM Para o container do banco COMPARTILHADO saas-postgres. Mantem os dados no volume.
REM ATENCAO: este banco e usado tambem por outros produtos (schema dentalflow).

setlocal
cd /d "%~dp0..\infra"
docker compose stop
endlocal

@echo off
rem Database commands, like php artisan: artisan migrate ^| migrate:fresh ^| db:show
rem Stop the admin and the website first.
cd /d "%~dp0"
dotnet run --project src\RcBunny.Backend -- %*

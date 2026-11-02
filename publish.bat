@echo off
REM Ferme PodsLink s'il tourne (sinon Windows verrouille l'exe a ecraser)
taskkill /IM PodsLink.exe /F >nul 2>&1
REM Exe autonome (aucun runtime a installer, ~150 Mo)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\autonome
REM Exe leger (necessite le runtime .NET 8 Desktop, ~quelques Mo)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist\leger
echo.
echo Termine : voir le dossier dist\
pause

@echo off
echo ==================================
echo EMBRATUR VR - Conversor de Videos
echo ==================================
echo.

REM Verifique se o FFMPEG está instalado
where ffmpeg >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERRO: FFMPEG nao encontrado! Por favor, instale o FFMPEG e adicione-o ao PATH.
    echo Download: https://www.gyan.dev/ffmpeg/builds/
    echo.
    pause
    exit /b
)

echo FFMPEG encontrado! Continuando...
echo.

:menu
echo Escolha o arquivo para converter:
echo 1. rio.mp4
echo 2. lencois.mp4
echo 3. pantanal.mp4
echo 4. Todos os arquivos
echo 5. Arquivo personalizado
echo 0. Sair
echo.

set /p opcao="Digite o numero da opcao desejada: "

if "%opcao%"=="1" (
    set "input_file=rio.mp4"
    set "output_file=rio.mp4"
    goto :converter
) else if "%opcao%"=="2" (
    set "input_file=lencois.mp4"
    set "output_file=lencois.mp4"
    goto :converter
) else if "%opcao%"=="3" (
    set "input_file=pantanal.mp4"
    set "output_file=pantanal.mp4"
    goto :converter
) else if "%opcao%"=="4" (
    goto :converter_todos
) else if "%opcao%"=="5" (
    goto :custom
) else if "%opcao%"=="0" (
    exit /b
) else (
    echo Opcao invalida!
    echo.
    goto :menu
)

:custom
echo.
set /p input_file="Digite o caminho do arquivo de entrada: "
set /p output_file="Digite o nome do arquivo de saida: "
goto :converter

:converter
echo.
echo Convertendo %input_file% para %output_file%...
echo.

REM Verifica se o arquivo de entrada existe
if not exist "%input_file%" (
    echo ERRO: Arquivo de entrada nao encontrado: %input_file%
    echo.
    pause
    goto :menu
)

REM Cria pasta de saída se não existir
if not exist "output" mkdir output

REM Executa a conversão otimizada para Meta Quest
ffmpeg -y -hwaccel cuda -hwaccel_output_format cuda -i "%input_file%" -c:v hevc_nvenc -preset p1 -tune hq -rc:v vbr_hq -b:v 12M -maxrate 15M -bufsize 20M -spatial-aq 1 -vf "scale_cuda=3072:1536" -c:a aac -b:a 128k -ac 2 "output\%output_file%"

echo.
echo Conversao concluida! Arquivo salvo em: output\%output_file%
echo.
pause
goto :menu

:converter_todos
echo.
echo Convertendo todos os arquivos...
echo.

REM Cria pasta de saída se não existir
if not exist "output" mkdir output

set arquivos=rio.mp4 lencois.mp4 pantanal.mp4

for %%f in (%arquivos%) do (
    if exist "%%f" (
        echo Convertendo %%f...
        ffmpeg -y -hwaccel cuda -hwaccel_output_format cuda -i "%%f" -c:v hevc_nvenc -preset p1 -tune hq -rc:v vbr_hq -b:v 12M -maxrate 15M -bufsize 20M -spatial-aq 1 -vf "scale_cuda=3072:1536" -c:a aac -b:a 128k -ac 2 "output\%%f"
        echo %%f convertido com sucesso!
    ) else (
        echo AVISO: Arquivo %%f nao encontrado, pulando...
    )
)

echo.
echo Conversao de todos os arquivos concluida!
echo Os arquivos foram salvos na pasta "output"
echo.
pause
goto :menu 
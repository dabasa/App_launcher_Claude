@echo off
setlocal

REM ImageMagick の magick コマンド確認
where magick >nul 2>nul
if errorlevel 1 (
    echo ImageMagick が見つかりません。
    echo PATH設定を確認してください。
    pause
    exit /b
)

REM D&Dされたファイルを処理
for %%F in (%*) do (

    REM ファイル名と拡張子取得
    set "INPUT=%%~fF"
    set "NAME=%%~nF"
    set "DIR=%%~dpF"

    REM 遅延展開ON
    setlocal enabledelayedexpansion

    REM 出力ファイル名
    set "OUTPUT=!DIR!!NAME!chg.png"

    echo 変換中: %%~nxF

    REM 64x64 PNGへ変換
    magick "%%~fF" -resize 64x64 "!OUTPUT!"

    echo 出力: !OUTPUT!

    endlocal
)

echo.
echo 完了しました。
pause
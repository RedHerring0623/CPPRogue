@echo off
setlocal

rem ============================================================
rem  CRogue 一键打包 Windows EXE
rem  用法：双击本文件（打包前请关闭团结引擎编辑器；开着 Hub 没关系）
rem  产物：CPPRogue\Builds\Windows\（整个文件夹才是游戏）
rem  注意：本文件必须保持 GBK 编码（中文 Windows cmd 的原生编码）
rem ============================================================

set "PROJECT=%~dp0CPPRogue"
set "EDITOR=E:\Tuanjie\Editor\Tuanjie.exe"
set "LOG=%~dp0build-exe.log"

if not exist "%EDITOR%" (
    echo [错误] 找不到团结引擎：%EDITOR%
    echo 请用记事本打开本脚本，把 EDITOR 变量改成你的安装路径。
    pause
    exit /b 1
)

if not exist "%PROJECT%\Assets" (
    echo [错误] 找不到 Unity 工程：%PROJECT%
    pause
    exit /b 1
)

rem 只拦"编辑器"：按路径判断，Tuanjie Hub 自带的 tuanjie.exe 辅助进程不算
powershell -NoProfile -Command "if (Get-Process Tuanjie -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\Editor\Tuanjie.exe' }) { exit 0 } else { exit 1 }"
if %errorlevel%==0 (
    echo [错误] 团结引擎编辑器正在运行，请先关闭它再打包（项目会被占用）。
    echo 提示：只开 Tuanjie Hub 不影响打包。
    pause
    exit /b 1
)

echo [1/2] 正在打包（首次较慢，可能要几分钟，请耐心等待）...
"%EDITOR%" -batchmode -quit -projectPath "%PROJECT%" -executeMethod CPPRogue.Tools.BuildScripts.BuildWindows -logFile "%LOG%"
set "EXITCODE=%errorlevel%"

echo.
if not "%EXITCODE%"=="0" (
    echo [失败] 打包退出码 %EXITCODE%，日志最后 30 行：
    echo ----------------------------------------
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG%' -Tail 30"
    echo ----------------------------------------
    echo 完整日志：%LOG%
    pause
    exit /b %EXITCODE%
)

echo [2/2] 打包成功！
echo 产物目录：%PROJECT%\Builds\Windows\
echo 提醒：分发时打包整个文件夹（exe + CPPRogue_Data + TuanjiePlayer.dll 等）。
start "" explorer "%PROJECT%\Builds\Windows"
pause

@echo off
REM ==========================================
REM Auto Git Commit & Merge Batch Wrapper
REM Windows Batch Script Wrapper
REM ==========================================

setlocal enabledelayedexpansion

:menu
cls
echo.
echo ============================================
echo   AUTO GIT COMMIT & MERGE TOOL
echo ============================================
echo.
echo Lựa chọn:
echo   1. Commit & Push (một lần)
echo   2. Commit & Merge & Push (một lần)
echo   3. Giám sát liên tục (Watch Mode)
echo   4. Giám sát và tự động merge
echo   5. Xem trạng thái Git hiện tại
echo   6. Thoát
echo.
set /p choice="Nhập lựa chọn (1-6): "

if "%choice%"=="1" goto commit_push
if "%choice%"=="2" goto commit_merge_push
if "%choice%"=="3" goto watch_mode
if "%choice%"=="4" goto watch_merge
if "%choice%"=="5" goto git_status
if "%choice%"=="6" goto exit_script

echo Lựa chọn không hợp lệ. Vui lòng thử lại.
timeout /t 2 >nul
goto menu

:commit_push
cls
echo.
echo 💾 Commit & Push Code...
echo.
set /p msg="Nhập commit message (mặc định: Auto-commit: Code update): "
if "%msg%"=="" set msg=Auto-commit: Code update
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto-git-commit.ps1" -CommitMessage "%msg%" -Push
pause
goto menu

:commit_merge_push
cls
echo.
echo 🔀 Commit & Merge & Push...
echo.
set /p source="Nhập source branch (mặc định: master): "
if "%source%"=="" set source=master
set /p target="Nhập target branch (mặc định: main): "
if "%target%"=="" set target=main
set /p msg="Nhập commit message (mặc định: Auto-commit: Code update): "
if "%msg%"=="" set msg=Auto-commit: Code update
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto-git-commit.ps1" -CommitMessage "%msg%" -SourceBranch "%source%" -TargetBranch "%target%" -Push
pause
goto menu

:watch_mode
cls
echo.
echo 👁️ Bắt đầu Watch Mode (Ctrl+C để dừng)...
echo.
set /p interval="Kiểm tra mỗi N giây (mặc định: 5): "
if "%interval%"=="" set interval=5
set /p msg="Nhập commit message (mặc định: Auto-commit: Code update): "
if "%msg%"=="" set msg=Auto-commit: Code update
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto-git-commit.ps1" -CommitMessage "%msg%" -Watch -WatchInterval %interval% -Push
goto menu

:watch_merge
cls
echo.
echo 👁️ Bắt đầu Watch Mode với Auto-Merge...
echo.
set /p source="Nhập source branch (mặc định: master): "
if "%source%"=="" set source=master
set /p target="Nhập target branch (mặc định: main): "
if "%target%"=="" set target=main
set /p interval="Kiểm tra mỗi N giây (mặc định: 5): "
if "%interval%"=="" set interval=5
set /p msg="Nhập commit message (mặc định: Auto-commit: Code update): "
if "%msg%"=="" set msg=Auto-commit: Code update
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto-git-commit.ps1" -CommitMessage "%msg%" -SourceBranch "%source%" -TargetBranch "%target%" -Watch -WatchInterval %interval% -Push
goto menu

:git_status
cls
echo.
echo 📊 Trạng thái Git hiện tại:
echo.
git status
echo.
pause
goto menu

:exit_script
echo.
echo 👋 Tạm biệt!
echo.
exit /b 0

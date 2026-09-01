@echo off
REM ---------------------------------------------------------------------------
REM  Copies the two files MCS needs onto the network install at Y:.
REM
REM  Only MCS.exe and Newtonsoft.Json.dll are copied, and they are always copied
REM  together - the exe records the exact Newtonsoft version it needs, so an
REM  install holding a mismatched DLL fails to start.
REM
REM  Database\, Posters\ and Log\ are never touched. Those are your catalogue,
REM  your posters and your settings.
REM ---------------------------------------------------------------------------

setlocal

set "SRC=%~dp0MovieSelector\bin\Release"
set "DST=Y:\Movies & Dramas\Movies\Movie Selector"

echo.
echo   From : %SRC%
echo   To   : %DST%
echo.

if not exist "%SRC%\MCS.exe" (
    echo   ERROR: MCS.exe not found.
    echo   Build the Release configuration in Visual Studio first.
    goto :done
)

if not exist "%SRC%\Newtonsoft.Json.dll" (
    echo   ERROR: Newtonsoft.Json.dll not found next to MCS.exe.
    echo   Rebuild the Release configuration in Visual Studio.
    goto :done
)

if not exist "%DST%\" (
    echo   ERROR: cannot reach the destination folder.
    echo   Is the Y: drive connected?
    goto :done
)

tasklist /fi "imagename eq MCS.exe" 2>nul | find /i "MCS.exe" >nul
if not errorlevel 1 (
    echo   ERROR: MCS.exe is still running. Close it and run this again.
    goto :done
)

echo   Copying...
copy /y "%SRC%\MCS.exe" "%DST%\" >nul
if errorlevel 1 goto :failed
copy /y "%SRC%\Newtonsoft.Json.dll" "%DST%\" >nul
if errorlevel 1 goto :failed

echo.
echo   Done. Both files updated.
goto :done

:failed
echo.
echo   COPY FAILED. Nothing further was copied.

:done
echo.
pause
endlocal

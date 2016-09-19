powershell -ExecutionPolicy Unrestricted -File build/release.ps1
if errorlevel 1 (
   echo Failure Reason Given is %errorlevel%
   exit /b %errorlevel%
)
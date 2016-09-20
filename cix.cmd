powershell -ExecutionPolicy Unrestricted -File build/cix.ps1
if errorlevel 1 (
   echo Failure Reason Given is %errorlevel%
   exit /b %errorlevel%
)
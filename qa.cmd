powershell -ExecutionPolicy Unrestricted -File build/qa.ps1
if errorlevel 1 (
   echo Failure Reason Given is %errorlevel%
   exit /b %errorlevel%
)
powershell -ExecutionPolicy Unrestricted -File qa.ps1
if errorlevel 1 (
   echo Failure Reason Given is %errorlevel%
   exit /b %errorlevel%
)
@echo off

if "%SIGNING_KEY_PASSPHRASE%"=="" (
	echo Environment variable SIGNING_KEY_PASSPHRASE is not set. This is required to decrypt strong name key
	exit /b 1
)

secure-file -decrypt RethinkFSharp.snk.enc -secret "%SIGNING_KEY_PASSPHRASE%"
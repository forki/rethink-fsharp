#!/bin/bash

if [ -z "${SIGNING_KEY_PASSPHRASE}" ];
then
	echo "Environment variable SIGNING_KEY_PASSPHRASE is not set. This is required to decrypt strong name key"
	exit 1
fi

if test "$OS" = "Windows_NT"
then
	secure-file -decrypt RethinkFSharp.snk.enc -secret "${SIGNING_KEY_PASSPHRASE}"
else
	mono secure-file -decrypt RethinkFSharp.snk.enc -secret "${SIGNING_KEY_PASSPHRASE}"
fi

if [ $? -ne 0 ];
then
    echo "failed to perform a paket restore"
	exit $?
fi
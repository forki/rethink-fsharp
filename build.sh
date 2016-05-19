#!/bin/bash

if test "$OS" = "Windows_NT"
then
	.paket/paket.bootstrapper.exe

	if [ $? -ne 0 ];
	then
	    echo "failed to run the paket.bootstrapper.exe"
		exit $?
	fi

	.paket/paket.exe restore

	if [ $? -ne 0 ];
	then
	    echo "failed to perform a paket restore"
		exit $?
	fi

	packages/Build/FAKE/tools/FAKE.exe build.fsx $@
else
	mono .paket/paket.bootstrapper.exe

	if [ $? -ne 0 ];
	then
	    echo "failed to run the paket.bootstrapper.exe"
	    exit $?
	fi

	.paket/paket.exe restore

	if [ $? -ne 0 ];
	then
	    echo "failed to perform a paket restore"
	    exit $?
	fi

	mono packages/Build/FAKE/tools/FAKE/exe build.fsx $@
fi
#!/usr/bin/env bash

mkdir -p /usr/lib/mono/xbuild-frameworks/.NETFramework/v4.6
dotnet restore && dotnet build src/**/project.json
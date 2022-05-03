#! /bin/bash

BUILDVERSION=${1:-./BuildVersion/BuildVersion.exe}

$BUILDVERSION \
        --verbose \
        --gitdir ../.git \
        --namespace org.herbal3d.cs.CommonEntitiesConv \
        --version $(cat VERSION) \
        --assemblyInfoFile Properties/AssemblyInfo.cs

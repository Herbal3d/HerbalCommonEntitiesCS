#! /bin/bash

if [[ ! -z "$1" ]] ; then
    export BUILDVERSION=${1}
fi
export BUILDVERSION=${BUILDVERSION:-./BuildVersion/BuildVersion.exe}

$BUILDVERSION \
        --verbose \
        --gitdir ../.git \
        --namespace org.herbal3d.cs.CommonEntities \
        --version $(cat VERSION) \
        --assemblyInfoFile Properties/AssemblyInfo.cs

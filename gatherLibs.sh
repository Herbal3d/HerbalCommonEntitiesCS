#! /bin/bash

OPENSIM=../../opensim-ssh/bin
# It is best to get the libomv dll's that OpenSim was built with
LIBOMV=../../opensim-ssh/bin
# LIBOMV=../../libopenmetaverse/bin

# Copy the dll file and the PDB file if it exists
function GetLib() {
    if [[ -z "$3" ]] ; then
        cp "$1/$2" libs
    else
        cp "$1/$2" "$3"
    fi
    pdbfile=$1/${2%.dll}.pdb
    if [[ -e "$pdbfile" ]] ; then
        cp "$pdbfile" libs
    fi
}

GetLib "$OPENSIM" "OpenSim.Framework.dll"
GetLib "$OPENSIM" "OpenSim.Region.CoreModules.dll"
GetLib "$OPENSIM" "OpenSim.Region.Framework.dll"
GetLib "$OPENSIM" "OpenSim.Region.PhysicsModules.SharedBase.dll"
GetLib "$OPENSIM" "OpenSim.Services.Interfaces.dll"

GetLib "$OPENSIM" "log4net.dll"
GetLib "$OPENSIM" "Nini.dll"

GetLib "$LIBOMV" "OpenMetaverse.dll"
GetLib "$LIBOMV" "OpenMetaverse.dll.config"
GetLib "$LIBOMV" "OpenMetaverseTypes.dll"
GetLib "$LIBOMV" "OpenMetaverse.StructuredData.dll"
GetLib "$LIBOMV" "OpenMetaverse.Rendering.Meshmerizer.dll"
GetLib "$LIBOMV" "PrimMesher.dll"

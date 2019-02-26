# HerbalCommonEntitiesCS

C# library of routines to convert, optimize, and write OpenSimulator world objects.

These sources are used in [Convoar] and [Loden] to convert OpenSimulator world
objects into a new structure designed for optimization and outputting to different
formats.

This code runs within either [OpenSimulator] (for [Loden]) or in a simulated
[OpenSimulator] environment (for [Convoar]). Because of this, the library DLLs
are not used directly but these projects are added to the build environment
of the application.

The operations available are:

* Convert [OpenSimulator] scene to "BScene" (CommonEntities internal format);
* Write out a BScene as Gltf file(s);
* More optimizations coming!!

This code expects parameters available with the ```CommonEntitiesUtil.IParameters```
interface. The expected parameters and values are:

| Parameter | Type | Description |
|-----------|:----:|-------------|
| OutputDir | string | directory to store assets and output files |
| URIBase | string | base of URIs created inside GLTF files
| GltfCopyright | string | copyright to include in the GLTF files |
| ConvoarID | string | UUID to use as 'owner' of created images/meshes |
| WriteBinaryGltf | Bool | whether to write GLB or GLTF
| | | |
| TextureMaxSize | int | maximum pixels for output texture dimensions (deprecated) |
| PreferredTextureFormat | string | one of GIF, PNG, JPG, BMP |
| PreferredTextureFormatIfNoTransparancy | string | format to output if any transparacy in image |
| DoubleSided   | Bool | true if to asset double sided in GLTF file |
| AddUniqueCodes | Bool | whether to include a unique hash code for entities in GLTF file |
| | | |
| AddTerrainMesh | Bool | whether to create and include a terrain mesh |
| CreateTerrainSplat | Bool | whether to create a terrain splat texture |
| HalfRezTerrain | Bool | whether to create a half-rez terrain mesh (128x128 rather than 256x256) |
| | | |
| VerticesMaxForBuffer | int | vertices split point for GLTF output (50000 is best number) |
| UseOpenJPEG | Bool | whether to use OpenJPEG to convert textures (deprecated) |
| DisplayTimeScaling | Bool | whether meshes are scaled at display time or now (should be 'false') |
| | | |
| SeparateInstancedMeshes | Bool | whether to separate instances of meshes |
| MergeSharedMaterialMeshes | Bool | whether to merge meshes that share a material |
| | | |
| LogBuilding | Bool | Output infomation about building
| LogGltfBuilding | Bool | Output information about Gltf creation


[OpenSimulator]: http://opensimulator.org/
[Convoar]: https://github.com/Misterblue/convoar
[Loden]:  https://github.com/Herbal3d/Loden

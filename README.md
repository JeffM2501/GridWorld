# Grid World

An experment in learning voxel rendering. Still very much a work in progress and mostly a learning endevor.

Copyright 2020 Jeffery Myers.

# Licenses
Code is MIT.

Assets are from https://www.kenney.nl/assets and PD.

# Language and Librarys
Written in C# (VC2019 .net 4.6.2), using UrhoSharp https://docs.microsoft.com/en-us/xamarin/graphics-games/urhosharp/introduction. 

Code should be crossplatform but it has not been tested.

Perlin Noise provided by LibNoise https://github.com/CalmBit/LibNoise.

All dependencies are in Nuget.

## Projects
* GridWorld is the main library that stores the voxel world
* GWT Test harnes and dynamic loading/unloading code

# Features
* 64 bit voxel indexing giving a world space of just under 2000 square lightyears if 1 unit = 1 meter
* Sliding origin to prevent floating point errors
* Supports static worlds or procedural generation (basic perlin for now)
* 16 bit dynamic block type indexes (only index the blocks types and shapes used)
* Multiple shapes including ramps
* Background threaded geometry generation/loading/unloading
* Near/Far geometry load/unload, stale node cleanup

# ToDo
* World Database
* Dynamic Cluster Loading
* 3D Cluster indexes
* Editing
* Strucutres
* Edit Layers
* Network Transmission
* Better Collisions
* Client Cleanup
* Vertex Shaders for fluids
* Mobs
* Triggers
* Block Sub Meshes
* More block types (Thin Verticals, Cutoff Corners, etc...)
* Fancy Procedural Generation

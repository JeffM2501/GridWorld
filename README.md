# Grid World

An experment in learning voxel rendering

Copyright 2020 Jeffery Myers

# Licenses
Codes is MIT.
Assets are from https://www.kenney.nl/assets and PD

# Language and Librarys
Written in C#, using UrhoSharp https://docs.microsoft.com/en-us/xamarin/graphics-games/urhosharp/introduction. 

## Projects

* GridWorld is the main library that stores the voxel world.
* GWT Test harnes and dynamic loading/unloading code.

# Features
* 64 bit voxel indexing giving a world space of just under 2000 square lightyears if 1 unit = 1 meter
* Sliding origin to prevent floating point errors
* Supports static worlds or procedural generation
* 16 bit dynamic block type indexes
* Multiple shapes including ramps
* Background geometry loading/unloading


# ToDo
* 3D Cluster indexes
* Editing
* Strucutres
* Layers
* Dynamic Cluster Loading
* Collision Verification
* Client Cleanup
* Fancy Procedural Generation
* Vertex Shaders for fluids
* Mobs
* Triggers
* Block Sub Meshes
* More block types (half verticals)

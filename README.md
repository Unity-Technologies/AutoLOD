# AutoLOD
Automatic LOD generation + scene optimization - Initial release was on January 12, 2018 via [blogpost](https://blogs.unity3d.com/2018/01/12/unity-labs-autolod-experimenting-with-automatic-performance-improvements/)

## Experimental Status
It’s important to note that AutoLOD is an experimental feature. As such, there is no formal support (e.g. FogBugz, support@unity3d.com, Premium Support, etc.) offered, so please do not use these channels. Instead, post your questions, comments, suggestions, and issues here on GitHub.

**As with anything experimental/preview/alpha/beta, it is always a good idea to make a backup of your project before using.**

Experimental means this:
- Namespaces, classes, software architecture, prefabs, etc. can change at any point. If you are writing your own tools, then you might need to update them as these things change.
- There won’t always be an upgrade path from one release to the next, so you might need to fix things manually, which leads to the next point...
- Stuff can and will break (!)
- There’s **no guarantee** that this project will move out of experimental status within any specific timeframe.
- As such, there is no guarantee that this will remain an actively supported project.

## Features
- LOD generation on model import with sensible [defaults](https://github.com/Unity-Technologies/AutoLOD/wiki/Home)
- Project-wide and per-model LOD import settings
- Asynchronous, pluggable LOD generation framework
- Hierarchical LOD support via [SceneLOD](https://github.com/Unity-Technologies/AutoLOD/wiki/Scenelod)

### Useful classes (for your own projects, too!)
- [MonoBehaviourHelper](Scripts/Helpers/MonoBehaviourHelper.cs) - a way to run coroutines in the editor + main thread execution from worker threads
- [LODGroupExtensions](Scripts/Extensions/LODGroupExtensions.cs) - useful extension methods (e.g. GetCurrentLOD)
- [TimedEnumerator](Scripts/Helpers/TimedEnumerator.cs) -  a way to control maximum execution time of coroutines
- [TextureAtlasModule](Scripts/Editor/TextureAtlasModule.cs) - automatically generate texture atlases
- [WorkingMesh](Scripts/Helpers/WorkingMesh.cs) - a thread-safe mesh (_and now job-friendly!_) struct

## Evaluating
Unity 2018.1 or a later version is required

### Git Dependencies
- [git-lfs](https://git-lfs.github.com/)
- [git-submodule](https://git-scm.com/docs/git-submodule)

### Cloning
1. Create a new Unity project or use an existing one
2. From the command line change directory to your project's `Assets` directory.
3. Run `git lfs clone --recursive https://github.com/Unity-Technologies/AutoLOD`

### Updating
Because this project uses [git-submodule](https://git-scm.com/docs/git-submodule), you'll need to execute `git submodule update` after pulling whenever a submodule is updated. You could execute this command always just to be safe or if you notice that a submodule is showing as modified after pulling changes.

Optionally, you could add a [git hook for post-checkout](https://ttboj.wordpress.com/2014/05/06/keeping-git-submodules-in-sync-with-your-branches/) or use a GUI (e.g. SourceTree) that does this automatically for you.

### Project Settings
If you plan on making changes to AutoLOD and/or contributing back, then you'll need to set the `Asset Serialization` property under Edit->Project Settings->Editor to `Force Text`.

## License
Unity Companion License (see [LICENSE](LICENSE))

## All contributions are subject to the [Unity Contribution Agreement (UCA)](https://unity3d.com/legal/licenses/Unity_Contribution_Agreement)
By making a pull request, you are confirming agreement to the terms and conditions of the UCA, including that your Contributions are your original creation and that you have complete right and authority to make your Contributions.

## Initial Contributors
[Amir Ebrahimi](https://github.com/amirebrahimi/)
<br>[Elliot Cuzzillo](https://github.com/ecuzzillo)
<br>[Yuangguang Liao](https://github.com/liaoyg)

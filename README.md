# AutoLOD
Automatic LOD generation + scene optimization - Initial release was on January 12, 2018 via [blogpost](https://blogs.unity3d.com/2018/01/12/unity-labs-autolod-experimenting-with-automatic-performance-improvements/)

AutoLOD is primarily a framework for enabling automatic post-processing of geometrical model assets on import to create simplified levels-of-detail (LOD). A [default mesh simplifier](https://github.com/Whinarn/UnityMeshSimplifier/) is included, but can be swapped out with other simplifiers and on a per-model basis if needed. Additionally, a whole scene can be hierarchically chunked into LODs with [SceneLOD](https://github.com/Unity-Technologies/AutoLOD/wiki/Scenelod).

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
- Asynchronous, pluggable LOD generation framework with built-in support for:
  - [UnityMeshSimplifier](https://github.com/Whinarn/UnityMeshSimplifier/)
  - [Simplygon](https://simplygon.com/)
  - [InstaLOD](https://instalod.com/)
- Hierarchical LOD support via [SceneLOD](https://github.com/Unity-Technologies/AutoLOD/wiki/Scenelod) -> [watch a [quick tutorial video](http://www.youtube.com/watch?v=EuBeZvzVwrw "SceneLOD Tutorial")]

### Useful classes (for your own projects, too!)
- [MonoBehaviourHelper](Scripts/Helpers/MonoBehaviourHelper.cs) - a way to run coroutines in the editor + main thread execution from worker threads
- [LODGroupExtensions](Scripts/Extensions/LODGroupExtensions.cs) - useful extension methods (e.g. GetCurrentLOD)
- [TimedEnumerator](Scripts/Helpers/TimedEnumerator.cs) -  a way to control maximum execution time of coroutines
- [TextureAtlasModule](Scripts/Editor/TextureAtlasModule.cs) - automatically generate texture atlases
- [WorkingMesh](Scripts/Helpers/WorkingMesh.cs) - a thread-safe mesh (_and now job-friendly!_) struct

## Evaluating
Unity 2018.4 (LTS) or a later version is required

### Install via package manager
- Using the UI
  1. Follow the steps provided [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html).
  2. The Git URL to use is `https://github.com/Unity-Technologies/AutoLOD.git`
- Manually through editing manifest.json
  1. Read the instructions from the official documentation [here](https://docs.unity3d.com/Manual/upm-git.html).
  2. Open up *manifest.json* inside the *Packages* directory in your Unity project using a text editor.
  3. Under the dependencies section of this file, you should add the following line at the top:
```"com.unity.autolod": "https://github.com/Unity-Technologies/AutoLOD.git",```
  1. You should now see something like this:
    ```json
    {
        "dependencies": {
            "com.unity.autolod": "https://github.com/Unity-Technologies/AutoLOD.git",
            "com.unity.ads": "2.0.8",
            "com.unity.analytics": "3.2.3",
            "com.unity.collab-proxy": "1.2.15",
            "...": "...",
        }
    }
    ```
  

### Cloning locally to your project (requires [git-lfs](https://git-lfs.github.com/))
1. Create a new Unity project or use an existing one
2. From the command line change directory to your project's `Packages` directory.
3. Run `git lfs clone https://github.com/Unity-Technologies/AutoLOD`

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

## Community Contributors
[@Camarent](https://github.com/Camarent), [@LoneDev6](https://github.com/LoneDev6), [@marwie](https://github.com/marwie), [@msellens](https://github.com/msellens), [@redwyre](https://github.com/redwyre)
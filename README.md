# Avatar Performance Tools

## 📦 https://vpm.thry.dev/ 📥 Add here with the latest VCC!

## [Discord Server for all my Assets](https://discord.thryrallo.de/)
 
## Avatar Evaluator
Calculates and evaluates some avatar metrics not currently taken into account by VRChat's ranking system.
1. VRAM Size
2. Grabpasses
3. Blendshapes
4. "Any State" transition count
5. Write defaults check
6. Empty animator state check
 
## VRAM Checker
Calculates the VRAM the textures on your avatar use.  
Please use this to keep your VRAM usage down. High VRAM usage causes performance problems.

#### Features
- Shows VRAM usage of only active and of all objects
- Lists textures, meshes, and their respective VRAM size
- Gives feedback on the VRAM size of your avatar
- Includes materials from animations
- Information Boxes regarding VRAM

## Installing using VRChat Creator Companion

1. Open VCC. Go  to Settings -> Packages -> Click the "Add Repository" Button (Next to Installed Repositories)
2. Paste the following URL and click the "Add" Button
    ```sh 
    vpm add repo https://thryrallo.github.io/VRC-Avatar-Performance-Tools
    ```
3. In Creator Compantion click "Manage Project". In the top right under "Selected Repos" check the Avatar Performance Tools listing

## Installing with UPM (Unity Package Manager)

### Using OpenUPM
To install the package using OpenUPM, follow the instructions in the top right of this page: https://openupm.com/packages/de.thryrallo.vrc.avatar-performance-tools/

If you already have OpenUPM installed, use this command in your project:
```sh
openupm add de.thryrallo.vrc.avatar-performance-tools
```


### As git package
Copy the git URL into the Unity Package Managers "Add Package from Git URL..." field.

`https://github.com/Thryrallo/VRC-Avatar-Performance-Tools.git`

![image](https://user-images.githubusercontent.com/31988415/209433908-b4f759c1-7ae4-4258-8aa4-7f45fed7489a.png)

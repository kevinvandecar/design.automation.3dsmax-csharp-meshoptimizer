# design.automation.3dsmax-csharp-meshoptimizer
## Design Automation for 3ds Max Sample 

![Platforms](https://img.shields.io/badge/Plugins-Windows-lightgray.svg)
![.NET 7](https://shields.io/badge/.NET%207-blue.svg)
![.NET 4.8](https://shields.io/badge/.NET%20Framework%204.8-blue.svg)
[![3dsMax](https://img.shields.io/badge/3ds%20Max-2024-00aaaa.svg)](http://developer.autodesk.com/)

## Description

This sample shows how to automate 3ds Max functionality using the Forge Design Automation APIs.

It is built from the base of the learn Forge tutorial.

It has a few components to handle the automation:
 
- .NET Framework plugin for **[3dsMax](ProOptimizerAutomation/)**. It automates setup and configuration of the ProOptimizer plugin. See readme for automation plugin details.
- .NET Core web interface to invoke Design Automation v3 and show results. See [readme](forgesample/) for more information.

The `designautomation.sln` includes the 3ds Max ProOptimizerAutomation bundles and the webapp. The `BUILD` action will copy all files to the bundle folder, generate a .zip and copy to the webapp folder. It requires [7-zip](https://www.7-zip.org/) tool. Additional to that, it will also make a copy to %ProgramData%/Autodesk/ApplicationPlugins for local testing.

NOTE!!!! I moved branches around to make it cleaer which one to use. The default "master" is now the latest and the one deployed as a sample. If you have any clones of this repo, please get a fresh copy. This was done when updating to the 3ds Max 2024 design automation engine.

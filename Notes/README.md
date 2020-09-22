# Environment Setup
  * Operating System: Ubuntu 18.04.5 LTS
  * Processor: Intel® Core™ i5-9300H CPU @ 2.40GHz × 8 
  * Steps
    * Install the [.NET Core SDK](https://dotnet.microsoft.com/download)
    * Install the [Ionide-fsharp extension for VSCode](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp)
    * *Also did the following steps (but these do not seem to be necessary for this project)* :thinking:
      * run ```dotnet new console --language F#```
      * run ```dotnet add package Akka.FSharp --version 1.4.10```
# Command Line
  * ```dotnet fsi --langversion:preview proj1.fsx N k```
  * Report time:
    * ```time dotnet fsi --langversion:preview proj1.fsx N k```

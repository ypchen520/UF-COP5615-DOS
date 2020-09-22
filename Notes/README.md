# COP5615 Fall 2020 Project 1 
  * Name: Yu-Peng Chen
  * UFID: 70943193
# Environment Setup
  * Operating System: Ubuntu 18.04.5 LTS
  * Processor: Intel® Core™ i5-9300H CPU @ 2.40GHz × 8 
  * __Steps__
    * Install the [.NET Core SDK](https://dotnet.microsoft.com/download)
    * Install the [Ionide-fsharp extension for VSCode](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp)
    * *Also did the following steps (but these do not seem to be necessary for this project)* :thinking:
      * run ```dotnet new console --language F#```
      * run ```dotnet add package Akka.FSharp --version 1.4.10```
# Command Line
  * ```dotnet fsi --langversion:preview proj1.fsx N k```
  * Report time:
    * ```time dotnet fsi --langversion:preview proj1.fsx N k```

# Report
  * Size of the work unit
    * __Number of workers: 8__
    * Size of work unit is decided by __dividing N by 8 (Number of workers)__, and the remainder goes to the last worker
      * For example, when N = 100, 100/8 = 12,
        * worker 1 gets the tasks starting with _1 to 12_
        * worker 2 gets the tasks starting with _13 to 24_
        * ...
        * worker 7 gets the tasks starting with _73 to 84_
        * worker 8 gets the tasks starting with _85 to 96_ plus the tasks starting with _97 to 100_
  * The result of running this program for ```dotnet fsi --langversion:preview proj1.fsx 1000000 4```
    * __Found nothing__ for N = 1000000, k = 4
    * ![Image]()
  * The running time of running this program for ```dotnet fsi --langversion:preview proj1.fsx 1000000 4```
    -------------|-------------
    real | 0m5.229s
    user | 0m6.251s
    sys | 0m0.521s	
  * The largest problem I managed to solve
    * ```dotnet fsi --langversion:preview proj1.fsx 100000000 2```

# Bonus
  * :dizzy_face: :exploding_head: :mask:

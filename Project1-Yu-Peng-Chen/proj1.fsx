#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

let system = ActorSystem.Create("FSharp")

type WorkerMessage = 
    | Work of int[]

type BossMessage = 
    | Assign of int[]
    | Finished of int

let isPerfectSquare (x: uint64) :bool =
    
    let sqrtX: uint64 = uint64(sqrt(double(x)))
    if sqrtX * sqrtX = x then true
    else false
 
let compute (start: int, nNums: int) = 
    let mutable sum : uint64 = 0UL
    for i = start to (start + nNums - 1) do
        let bigNum : uint64 = uint64 i
        sum <- sum + (bigNum * bigNum)
    // printfn "start: %d sum: %d" start sum
    if isPerfectSquare(sum) then start
    else 0

let worker (workerMailbox:Actor<WorkerMessage>) = 
    let rec loop () = actor {
        let! (msg: WorkerMessage) = workerMailbox.Receive()
        match msg with
        | Work msg->                                     
            let startPoint: int = msg.[0]
            let endPoint: int = msg.[1]
            let nNums: int = msg.[2]
            for i = startPoint to endPoint do
                let ans: int = compute(i, nNums)
                // if ans <> 0 then
                //     printfn "%d" ans
                workerMailbox.Sender() <! Finished ans
        return! loop ()
    }
    loop ()

let worker1 = spawn system "Worker1" worker
let worker2 = spawn system "Worker2" worker
let worker3 = spawn system "Worker3" worker
let worker4 = spawn system "Worker4" worker
let worker5 = spawn system "Worker5" worker
let worker6 = spawn system "Worker6" worker
let worker7 = spawn system "Worker7" worker
let worker8 = spawn system "Worker8" worker

let boss = 
    spawn system "Boss"
        <| fun bossMailbox ->
            let mutable nWorkLeft = 0
           
            let rec bossLoop() =             
                actor {
                    let! (msg: BossMessage) = bossMailbox.Receive()
                    match msg with
                    | Assign msg ->
                        nWorkLeft <- msg.[0] 
                        let n = msg.[0]
                        let nNums = msg.[1]
                        let nWorkers = msg.[2]

                        let workLoad = n / nWorkers
                        let start1: int = 1
                        let start2: int = start1 + workLoad
                        let start3: int = start2 + workLoad
                        let start4: int = start3 + workLoad
                        let start5: int = start4 + workLoad
                        let start6: int = start5 + workLoad
                        let start7: int = start6 + workLoad
                        let start8: int = start7 + workLoad
                        
                        worker1 <! Work [|(start1); (start2-1); nNums|]
                        worker2 <! Work [|(start2); (start3-1); nNums|]
                        worker3 <! Work [|(start3); (start4-1); nNums|]
                        worker4 <! Work [|(start4); (start5-1); nNums|]
                        worker5 <! Work [|(start5); (start6-1); nNums|]
                        worker6 <! Work [|(start6); (start7-1); nNums|]
                        worker7 <! Work [|(start7); (start8-1); nNums|]
                        worker8 <! Work [|(start8); n; nNums|]        
                                                   
                    | Finished msg -> 
                        // printfn "work done! %d" nWorkLeft
                        nWorkLeft <- nWorkLeft - 1
                        if msg <> 0 then
                            printfn "%d" msg
                        if nWorkLeft = 0 then
                            Environment.Exit 1                   
                       
                    return! bossLoop()        
                } 
            bossLoop()


let main() = 
    let args = fsi.CommandLineArgs
    let nMessages : int = int args.[1]
    let nNums : int = int args.[2]
    let nWorkers : int = int 8
    let param = [|nMessages; nNums; nWorkers|]
    for timeout in [1000000] do
        try
            let task = (boss <? Assign param)
            Async.RunSynchronously (task, timeout)
        with :? TimeoutException ->
            printfn "ask: timeout!"
    0

main()

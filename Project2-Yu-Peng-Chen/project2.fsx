#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

// hyperparameters
let system = ActorSystem.Create("Gossip")
let magicNum = 10
let tinyChange: double = 10.0 ** -10.0
let magicRound = 5
let pushGoal: string = "avg"
let gossipPeriod = 300

type BossMessage = 
    | GossipBegins of int * string * int
    | GossipReceived of int
    | Gossiping of string
    | GossipDone of int * int
    | PushBegins of int * string
    | Pushing of bool
    | PushDone of float

type NodeMessage =
    | Init of int [] * int * string * bool
    | Gossip of string
    | GossipTrigger of string
    | Push of double * double
    | PushTrigger of bool

let buildFullNetwork(network: byref<_>, numNodes: int) = 
    for i = 0 to numNodes-1 do
        let mutable arr: int [] = Array.empty
        for j = 0 to numNodes-1 do
            if i <> j then arr <- Array.append arr [|j|]
        network <- Array.append network [|arr|]

let build2DNetwork(network: byref<_>, numNodes: int) =
    let sqrtNumNodes = int(ceil(sqrt(float(numNodes))))
    for i = 0 to numNodes - 1 do
        let mutable arr: int [] = Array.empty
        if (i - sqrtNumNodes) >= 0 then arr <- Array.append arr [|i - sqrtNumNodes|]
        if (i + sqrtNumNodes) <= numNodes - 1 then arr <- Array.append arr [|i + sqrtNumNodes|]
        if (i % sqrtNumNodes) <> 0 then arr <- Array.append arr [|i-1|]
        if (((i+1) % sqrtNumNodes) <> 0) && ((i+1) < numNodes) then arr <- Array.append arr [|i+1|]
        network <- Array.append network [|arr|]

let buildLineNetwork(network: byref<_>, numNodes: int) =
    for i = 0 to numNodes - 1 do
        if i = 0 then network <- Array.append network [|[|i+1|]|]
        elif i > 0 && i < numNodes-1 then network <- Array.append network [|[|i-1; i+1|]|]
        elif i = numNodes - 1 then network <- Array.append network [|[|i-1|]|]

let buildImp2DNetwork(network: byref<_>, numNodes: int) =
    let sqrtNumNodes = int(ceil(sqrt(float(numNodes))))
    let rand = new System.Random()
    for i = 0 to numNodes - 1 do
        let mutable arr: int [] = Array.empty
        if (i - sqrtNumNodes) >= 0 then arr <- Array.append arr [|i - sqrtNumNodes|]
        if (i + sqrtNumNodes) <= numNodes - 1 then arr <- Array.append arr [|i + sqrtNumNodes|]
        if (i % sqrtNumNodes) <> 0 then arr <- Array.append arr [|i-1|]
        if (((i+1) % sqrtNumNodes) <> 0) && ((i+1) < numNodes) then arr <- Array.append arr [|i+1|]
        let mutable randNeighbor = rand.Next(numNodes)
        let seqFromArr = arr :> seq<int>
        let mutable isExist = Seq.exists ((=)randNeighbor) seqFromArr 
        while isExist || (randNeighbor = i) do
            randNeighbor <- rand.Next(numNodes)
            isExist <- Seq.exists ((=)randNeighbor) seqFromArr
        arr <- Array.append arr [|randNeighbor|]
        network <- Array.append network [|arr|]

let node (nodeMailbox: Actor<NodeMessage>)=
    //Gossip init
    let mutable nodeIndex: int = -1
    let mutable count: int = 0
    let mutable neighborsArr: int [] = Array.empty

    //Push-sum init
    let mutable s: double = 0.0
    let mutable w: double = 1.0
    let mutable currRatio: double = 0.0
    let mutable pushCount: int = 1
    let mutable isConverge: bool = false

    //For both
    let bossActor = select ("akka://Gossip/user/gossipBoss") system
    let rand = new System.Random()
    let mutable isFailed: bool = false

    let rec loop() = actor {
        let! (msg: NodeMessage) = nodeMailbox.Receive()
        match msg with
        | Init (neighbors, index, pushType, failure)->
            neighborsArr <- Array.append neighborsArr neighbors
            nodeIndex <- index
            s <- double(index)
            if failure then 
                isFailed <- true
            if pushType = "sum" then
                if index <> 0 then
                    w <- 0.0
        | Gossip gossipMsg ->
            if isFailed then
                count <- magicNum
            if count < magicNum then
                let randNeighbor: int = rand.Next(neighborsArr.Length)
                let nextNeighbor: int = neighborsArr.[randNeighbor]
                let neighborActor = select ("akka://Gossip/user/node" + string nextNeighbor) system
                // tell the boss that this node can start gossiping periodically
                if count = 0 then    
                    bossActor <! GossipReceived nodeIndex
                count <- count + 1
                // printfn "%d %d" nodeIndex count
                neighborActor <! Gossip gossipMsg
            else if count = magicNum then 
                bossActor <! GossipDone (nodeIndex, count)
                
        | GossipTrigger gossipMsg->
            if count < magicNum then
                let randNeighbor: int = rand.Next(neighborsArr.Length)
                let nextNeighbor: int = neighborsArr.[randNeighbor]
                let neighborActor = select ("akka://Gossip/user/node" + string nextNeighbor) system
                neighborActor <! Gossip gossipMsg
            
        | Push (sum, weight) ->
            if not isConverge then
                s <- double(s + sum)
                w <- double(w + weight)
                // printfn "%.10f %.10f" s w
        | PushTrigger push ->
            if isFailed then
                isConverge <- true
            if not isConverge then
                let mutable ratio: double = double (s / w)
                let diff: double = abs(double(currRatio) - double(ratio))
                // printfn "%.10f" diff
                if diff <= tinyChange then
                    //printfn "%.10f" x
                    pushCount <- pushCount + 1
                    if pushCount = magicRound then
                        // printfn "Done: %d" nodeIndex
                        isConverge <- true
                        bossActor <! PushDone ratio
                else
                    pushCount <- 0
                currRatio <- ratio
                let randNeighbor: int = System.Random().Next(neighborsArr.Length)
                let nextNeighbor: int = neighborsArr.[randNeighbor]
                let neighborActor = select ("akka://Gossip/user/node" + string nextNeighbor) system
                neighborActor <! Push (double(s * 0.5), double(w * 0.5))
                nodeMailbox.Self.Tell(Push (double(s * 0.5), double(w * 0.5)))
                s <- 0.0
                w <- 0.0
                // s <- double(s * 0.5)
                // w <- double(w * 0.5)

        return! loop()
    }
    loop()

let gossipBoss (bossMailbox: Actor<BossMessage>) =
    //things to keep track of as a boss
    let mutable actNodeArr: int [] = Array.empty
    let mutable numNodes: int = 0
    let mutable count: int = 0 
    
    // boss timer
    let bossTimer = new System.Diagnostics.Stopwatch()

    let rec loop() = actor {
        let! (msg: BossMessage) = bossMailbox.Receive()    
        match msg with
        | GossipBegins (nodeIndex, gossipMsg, number)->
            let nodeAct = select ("akka://Gossip/user/node" + string nodeIndex) system
            numNodes <- number
            bossTimer.Start()
            nodeAct <! Gossip "Fire!"
        | GossipReceived index ->
            actNodeArr <- Array.append actNodeArr [|index|]
        | Gossiping gossipMsg ->
            if actNodeArr.Length <> 0 then 
                for index in actNodeArr do
                    let nodeAct = select ("akka://Gossip/user/node" + string index) system
                    nodeAct <! GossipTrigger gossipMsg     
        | GossipDone (index, msgNum) ->
            actNodeArr <- Array.filter ((<>) index) actNodeArr
            count <- count + 1
            if msgNum <> magicNum then
                printfn("Something funny is going on")
            if count = numNodes then
                printfn "Finished gossiping. The time taken: %i" bossTimer.ElapsedMilliseconds
                Environment.Exit 1
        | PushBegins (number, pushType) ->
            numNodes <- number
            bossTimer.Start()
            for i = 0 to number - 1 do
                let nodeAct = select ("akka://Gossip/user/node" + string i) system
                if pushType = "avg" then
                    nodeAct <! Push (double(i), 1.0)
                else if pushType = "sum" then
                    if i = 0 then
                        nodeAct <! Push (double(i), 1.0)
                    else
                        nodeAct <! Push (double(i), 0.0)
        | Pushing push ->
            for i = 0 to numNodes - 1 do
                let nodeAct = select ("akka://Gossip/user/node" + string i) system
                nodeAct <! PushTrigger true
        | PushDone ratio->
            count <- count + 1
            printfn "%.10f" ratio
            if count = numNodes then
                printfn "Finished push-sum. The time taken: %i" bossTimer.ElapsedMilliseconds
                Environment.Exit 1     
        return! loop()
    }
    loop()

let main() = 
    // command line args
    try
        let args = fsi.CommandLineArgs
        let numNodes : int = int args.[1]
        let topology : string = string args.[2]
        let algorithm : string = string args.[3]
        // let fRate : int = int args.[4]
    
        // central info
        let timer = new System.Diagnostics.Stopwatch()
        let mutable network: int [] [] = Array.empty
        let gossipBoss = spawn system "gossipBoss" gossipBoss
        let nodeActArr = Array.init numNodes (fun index -> spawn system ("node" + string index) node)
    
        // start the timer and record the first timestamp
        timer.Start()
        let mutable lastTimestamp = timer.ElapsedMilliseconds
 
        // build the topology
        match topology with
            | "full" -> buildFullNetwork(&network, numNodes)
            | "2D" -> build2DNetwork(&network, numNodes)
            | "line" -> buildLineNetwork(&network, numNodes)
            | "imp2D" -> buildImp2DNetwork(&network, numNodes)
            | _ -> 
                printfn "Topology not recognized"
                Environment.Exit 1   

        printfn "Topology successfully built!! The time taken: %i" timer.ElapsedMilliseconds
        
        // // Calculate the index of nodes that are going to fail
        // let rand = new System.Random()
        // let fNum: int = numNodes * fRate / 100
        // let mutable fSeq: seq<int> = Seq.empty
        // for i = 0 to fNum-1 do
        //     let mutable randNode = rand.Next(numNodes)
        //     let mutable isExist = Seq.exists ((=)randNode) fSeq 
        //     while isExist do
        //         randNode <- rand.Next(numNodes)
        //         isExist <- Seq.exists ((=)randNode) fSeq
        //     fSeq <- Seq.append fSeq [randNode]
            
        for i = 0 to numNodes - 1 do
            nodeActArr.[i] <! Init (network.[i], i, pushGoal, false)
            // let isFailure = Seq.exists ((=)i) fSeq
            // if isFailure then
            //     nodeActArr.[i] <! Init (network.[i], i, pushGoal, true)
            // else
            //     nodeActArr.[i] <! Init (network.[i], i, pushGoal, false)

        match algorithm with
            | "gossip" -> 
                gossipBoss <! GossipBegins (0, "Fire!", numNodes)
                // simulate participants who know the message gossiping
                lastTimestamp <- timer.ElapsedMilliseconds
                while true do
                    if (timer.ElapsedMilliseconds - lastTimestamp) >= int64(gossipPeriod) then
                        lastTimestamp <- timer.ElapsedMilliseconds
                        gossipBoss <! Gossiping "Fire!"

            | "push-sum" -> 
                gossipBoss <! PushBegins (numNodes, pushGoal)
                lastTimestamp <- timer.ElapsedMilliseconds
                while true do
                    if (timer.ElapsedMilliseconds - lastTimestamp) >= int64(gossipPeriod) then
                        lastTimestamp <- timer.ElapsedMilliseconds
                        gossipBoss <! Pushing true
            | _ -> 
                printfn "Algorithm not recognized"
                Environment.Exit 1   
    with :? IndexOutOfRangeException ->
        printfn "Wrong number of input arguments! Please refer to the following format:"
        printfn "dotnet fsi --langversion:preview proj2.fsx [NUMBER OF NODES] [TOPOLOGY] [ALGORITHM]"

    0

main()
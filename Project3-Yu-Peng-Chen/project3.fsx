#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open Akka.TestKit

// hyperparameters
let git : bool = true
let system = ActorSystem.Create("Pastry")
let b : int = 3
let l : int = 16
let nodeIdLen: int = 1 <<< b

type BossMessage = 
    | Init of bool
    | InitJoin of string
    | Joined of string
    | Trigger of bool
    | Finished of int

type PeerMessage = 
    | First of string
    | Join of string
    | Add of string * string [,] * int [] * int [] * int
    | NextPeer of ActorSelection * string [,] * int [] * int [] * int
    | Deliver of string [,] * int [] * int []
    | Route of string
    | Forward of string * int * int

let shl(nodeIdA : string, nodeIdB : string) : int = 
    let prefixLen = Math.Min(String.length(nodeIdA), String.length(nodeIdB))
    let mutable i : int = 0
    while i < prefixLen && nodeIdA.[i] = nodeIdB.[i] do
        i <- i + 1
    i

let convertToId(num : int) : string = 
    let mutable id : string = Convert.ToString(num, nodeIdLen)
    let mutable temp : string = ""
    for i = String.length(id) to nodeIdLen - 1 do
        temp <- temp + "0"
    id <- temp + id
    id

let getId(index : int) : string =
    let rand = new System.Random()
    let mutable nodeId : string = Convert.ToString(index, nodeIdLen)
    let mutable temp : string = ""
    for i = String.length(nodeId) to nodeIdLen - 1 do
        temp <- temp + "0"
        let mutable nextDigit : string = string(rand.Next(nodeIdLen))
        nodeId <- nodeId + nextDigit
    nodeId

let getKey(index : int) : string =
    let rand = new System.Random()
    let mutable key : string = ""
    for i = 0 to nodeIdLen - 1 do
        let mutable nextDigit : string = string(rand.Next(nodeIdLen))
        key <- key + nextDigit
    key

let route(curr : string, dest :string, level : int, context : string, lLeaf : int [], sLeaf : int [], rTable : string[,]) : string =
    let mutable isFound : bool = false
    let mutable isNextThere : bool = false
    let mutable nextPeer : string = "mr.right"
    let mutable decNextPeer : int = -1
    let mutable decDest : int = Convert.ToInt32(dest, nodeIdLen)
    let mutable decCurr : int = Convert.ToInt32(curr, nodeIdLen)
    let mutable minDiff : int = abs(decDest - decCurr)

    if level = nodeIdLen then
        // printfn "exactly the same: %d" level
        nextPeer <- null
        isFound <- true
    
    // search in the leaves
    if not isFound then
        if decDest > decCurr then
            if lLeaf.Length <> 0 then
                if context = "join" then
                    if (decDest < lLeaf.[lLeaf.Length - 1]) then
                        for i = 0 to lLeaf.Length - 1 do
                            if lLeaf.[i] < decDest then
                                decNextPeer <- lLeaf.[i]
                elif context = "route" then
                    if (decDest <= lLeaf.[lLeaf.Length - 1]) then
                        for i = 0 to lLeaf.Length - 1 do
                            if lLeaf.[i] <= decDest then
                                decNextPeer <- lLeaf.[i]
            if decNextPeer <> -1 then
                if git then
                    nextPeer <- convertToId(decNextPeer)
                else
                    nextPeer <- Convert.ToString(decNextPeer, nodeIdLen)
                isFound <- true
        elif decDest < decCurr then
            if sLeaf.Length <> 0 then
                if context = "join" then
                    if (decDest > sLeaf.[sLeaf.Length - 1]) then
                        for i = 0 to sLeaf.Length - 1 do
                            if sLeaf.[i] < decDest then
                                decNextPeer <- sLeaf.[i]
                elif context = "route" then
                    if (decDest >= sLeaf.[sLeaf.Length - 1]) then
                        for i = 0 to sLeaf.Length - 1 do
                            if sLeaf.[i] <= decDest then
                                decNextPeer <- sLeaf.[i]
            if decNextPeer <> -1 then
                if git then
                    nextPeer <- convertToId(decNextPeer)
                else
                    nextPeer <- Convert.ToString(decNextPeer, nodeIdLen)
                isFound <- true

    // search in the route table
    if not isFound then
        let col : int = int(string(dest.[level]))
        if rTable.[level, col] <> null then
            let decId : int = Convert.ToInt32(rTable.[level, col], nodeIdLen)
            if context = "join" then
                if decId < decDest then
                    nextPeer <- rTable.[level, col]
                else
                    nextPeer <- null
                isFound <- true
            elif context = "route" then
                if decId <= decDest then
                    nextPeer <- rTable.[level, col]
                    isFound <- true
    // rare case
    if not isFound then
        let mutable union : int [] = Array.empty
        for i = 0 to lLeaf.Length-1 do
            let lLeafId = convertToId(lLeaf.[i])
            // let lLeafId = Convert.ToString(lLeaf.[i], nodeIdLen)
            if (shl(lLeafId, dest) >= level) then
                union <- Array.append union [|lLeaf.[i]|]
        for i = 0 to sLeaf.Length-1 do
            let sLeafId = convertToId(sLeaf.[i])
            // let sLeafId = Convert.ToString(sLeaf.[i], nodeIdLen)
            if (shl(sLeafId, dest) >= level) then
                union <- Array.append union [|sLeaf.[i]|]
        for i = 0 to rTable.[*,0].Length-1 do
            for j = 0 to rTable.[i,*].Length-1 do
                if rTable.[i,j] <> null then
                    let decRT : int = Convert.ToInt32(rTable.[i,j], nodeIdLen)
                    union <- Array.append union [|decRT|]
        for decPeer in union do
            if decPeer <> decDest then
                let diff : int = abs(decDest - decPeer)
                if diff < minDiff then
                    minDiff <- diff
                    decNextPeer <- decPeer
                    isFound <- true
        if isFound then
            nextPeer <- convertToId(decNextPeer)
            // nextPeer <- Convert.ToString(decNextPeer, nodeIdLen)

    if not isFound then
        nextPeer <- null
        isFound <- true  

    nextPeer


let updateNewRT(rT : byref<string [,]>, curr : string, dest : string, level : int) =
    let mutable col : int = int(string(curr.[level]))
    if rT.[level, col] <> null then
        let decRT = Convert.ToInt32(rT.[level, col], nodeIdLen)
        let decCurr = Convert.ToInt32(curr, nodeIdLen)
        if decRT < decCurr then
            rT.[level, col] <- curr
    else
        rT.[level, col] <- curr

    col <- int(string(dest.[level]))
    rT.[level, col] <- null

let updateRT(rTable : byref<string [,]>, dest : string, level : int) = 
    let col : int = int(string(dest.[level]))
    rTable.[level, col] <- dest

let updateLeaf(lLeaf : byref<int []>, sLeaf : byref<int []>, curr : string, dest : string, level : int) = 
    let decDest = Convert.ToInt32(dest, nodeIdLen)
    let decCurr = Convert.ToInt32(curr, nodeIdLen)
    let mutable isLFull : bool = false
    let mutable isSFull : bool = false

    if lLeaf.Length = l / 2 then
        isLFull <- true
    if sLeaf.Length = l / 2 then
        isSFull <- true

    if decDest > decCurr then
        let seqFromArr = lLeaf :> seq<int>
        let isExist = Seq.exists ((=)decDest) seqFromArr 
        if not isExist then
            if isLFull then 
                lLeaf <- Array.append lLeaf [|decDest|]
                lLeaf <- Array.sort lLeaf
                lLeaf <- lLeaf.[0..(lLeaf.Length-2)]
            else
                lLeaf <- Array.append lLeaf [|decDest|]
                lLeaf <- Array.sort lLeaf
    elif decDest < decCurr then
        let seqFromArr = sLeaf :> seq<int>
        let isExist = Seq.exists ((=)decDest) seqFromArr 
        if not isExist then
            if isSFull then 
                sLeaf <- Array.append sLeaf [|decDest|]
                sLeaf <- Array.sort sLeaf
                sLeaf <- sLeaf.[1..(sLeaf.Length-1)]
            else
                sLeaf <- Array.append sLeaf [|decDest|]
                sLeaf <- Array.sort sLeaf
            
let updateLL(lLeaf : byref<int []>, decCurr : int, dest : string) = 
    let decDest = Convert.ToInt32(dest, nodeIdLen)
    // let decCurr = Convert.ToInt32(curr, nodeIdLen)
    let mutable isLFull : bool = false
    if lLeaf.Length = l / 2 then
        isLFull <- true
    if decCurr > decDest then
        let seqFromArr = lLeaf :> seq<int>
        let isExist = Seq.exists ((=)decCurr) seqFromArr 
        if not isExist then
            if isLFull then 
                lLeaf <- Array.append lLeaf [|decCurr|]
                lLeaf <- Array.sort lLeaf
                lLeaf <- lLeaf.[0..(lLeaf.Length-2)]
            else
                lLeaf <- Array.append lLeaf [|decCurr|]
                lLeaf <- Array.sort lLeaf
    
let updateSL(sLeaf : byref<int []>, decCurr : int, dest : string) = 
    let decDest = Convert.ToInt32(dest, nodeIdLen)
    // let decCurr = Convert.ToInt32(curr, nodeIdLen)
    let mutable isSFull : bool = false
    if sLeaf.Length = l / 2 then
        isSFull <- true
    if decCurr > decDest then
        let seqFromArr = sLeaf :> seq<int>
        let isExist = Seq.exists ((=)decCurr) seqFromArr 
        if not isExist then
            if isSFull then 
                sLeaf <- Array.append sLeaf [|decCurr|]
                sLeaf <- Array.sort sLeaf
                sLeaf <- sLeaf.[1..sLeaf.Length]
            else
                sLeaf <- Array.append sLeaf [|decCurr|]
                sLeaf <- Array.sort sLeaf

let peer (nodeId : string) (numNodes : int) (numRequests : int) (rowNum : int) (peerMailbox: Actor<PeerMessage>) =
    let nId : string = nodeId
    let numN : int = numNodes
    let numR : int = numRequests
    let mutable routeTable = Array2D.zeroCreate<string> nodeIdLen nodeIdLen
    let mutable largeLeaf : int [] = Array.empty
    let mutable smallLeaf : int [] = Array.empty
    let bossActor = select ("akka://Pastry/user/pastryBoss") system
    // let mutable IDArr : string [] = Array.empty
    let rec loop() = actor {
        let! (msg: PeerMessage) = peerMailbox.Receive()    
        match msg with
        | First firstNodeId -> 
            // printfn "[peer] First %s" nId
            peerMailbox.Sender() <! Joined nId
        | Join nextPeer ->
            // printfn "[peer] %s Join %s" nId nextPeer
            let nearestPeerActor = select ("akka://Pastry/user/" + nextPeer) system
            nearestPeerActor <! Add (nId, routeTable, largeLeaf, smallLeaf, 0)
        | Add (dest, rTable, lLeaf, sLeaf, level) ->
            let mutable isLastHop : bool = false
            let mutable rT = Array2D.copy rTable
            let mutable lL = Array.copy lLeaf
            let mutable sL = Array.copy sLeaf
            let thisLevel : int = shl(dest, nId)
            if smallLeaf.Length = 0 && largeLeaf.Length = 0 then
                isLastHop <- true
            // printfn "[%s] %A" nId routeTable
            let nextPeer =  route(nId, dest, thisLevel, "join", largeLeaf, smallLeaf, routeTable) //string
            // printfn "[peer] %s add %s" nId nextPeer
            if nextPeer = null then
                isLastHop <- true
            updateLeaf(&largeLeaf, &smallLeaf, nId, dest, thisLevel)
            updateRT(&routeTable, dest, thisLevel)
            for i = level to thisLevel do
                for j = 0 to (routeTable.[thisLevel,*].Length - 1) do
                    if routeTable.[i,j] <> null then
                        rT.[i,j] <- routeTable.[i,j]
            updateNewRT(&rT, nId, dest, thisLevel)
            let decNodeId : int = Convert.ToInt32(nId, nodeIdLen)
            updateLL(&lL, decNodeId, dest)
            updateSL(&sL, decNodeId, dest)
            for leaf in largeLeaf do
                updateLL(&lL, leaf, dest)
                updateSL(&sL, leaf, dest)
            for leaf in smallLeaf do
                updateLL(&lL, leaf, dest)
                updateSL(&sL, leaf, dest)
            if not isLastHop then
                let nextPeer = select ("akka://Pastry/user/" + string nextPeer) system
                peerMailbox.Sender() <! NextPeer (nextPeer, rT, lL, sL, thisLevel)
            else
                peerMailbox.Sender() <! Deliver (rT, lL, sL)
        | NextPeer (nPeer, rTable, lLeaf, sLeaf, level) -> 
            routeTable <- Array2D.copy rTable
            largeLeaf <- Array.copy lLeaf
            smallLeaf <- Array.copy sLeaf
            nPeer <! Add (nId, routeTable, largeLeaf, smallLeaf, level)
        | Deliver (rTable, lLeaf, sLeaf) ->
            // printfn "[%s] delivers %A %A" nId rTable sLeaf
            routeTable <- Array2D.copy rTable
            largeLeaf <- Array.copy lLeaf
            smallLeaf <- Array.copy sLeaf
            bossActor <! Joined nId
        | Route msg ->
            let key : string = getKey(numN)
            let level = shl(key, nId)
            peerMailbox.Self.Tell(Forward (key, level, 0))
            peerMailbox.Sender() <! Trigger true      
        | Forward (dest, level, numHops) ->
            let mutable nHops : int = numHops
            let nextPeerId = route(nId, dest, level, "route", largeLeaf, smallLeaf, routeTable) //string
            // printfn "[%s] next: %s" nId nextPeerId
            if nextPeerId = null then
                // printfn "[%s] finished: %d" nId nHops
                bossActor <! Finished nHops
            else
                nHops <- nHops + 1
                let thisLevel = shl(dest, nextPeerId)
                let nextPeer = select ("akka://Pastry/user/" + string nextPeerId) system
                nextPeer <! Forward (dest, thisLevel, nHops)
        return! loop()
    }
    loop()

let pastryBoss numNodes numRequests (bossMailbox: Actor<BossMessage>) =
    //things to keep track of as a boss
    let numN : int = numNodes
    let numR : int = numRequests
    let rowNum : int = int(ceil(log10(double(numNodes))/log10(double(nodeIdLen))))
    let mutable index : int = 1
    let mutable firstNodeId : string = ""
    // let mutable peerArr : IActorRef [] = Array.empty
    let mutable peerArr : IActorRef [] = Array.empty
    let mutable count: int = 0
    let mutable tCount: int = 0
    let mutable totalHops : double = 0.0
    let totalRequests : int = numNodes * numRequests
    let mutable IDArr : string [] = Array.empty
    let mutable trCount : int = 0
    let mutable reqCount : int = 0

    let rec loop() = actor {
        let! (msg: BossMessage) = bossMailbox.Receive()    
        match msg with
        | Init init ->
            firstNodeId <- getId(index)
            IDArr <- Array.append IDArr [|firstNodeId|]
            // printfn "[Fisrt] %s" firstNodeId
            let peerActor = spawn system firstNodeId (peer firstNodeId numN numR rowNum)
            peerArr <- Array.append peerArr [|peerActor|]
            index <- index + 1
            peerActor <! First firstNodeId
            
        | InitJoin jNodeId ->
            if index > 1 && index <= numN then
                // create nodes
                let mutable nodeId : string = getId(index)
                // check for duplicate IDs
                let IDSeq = IDArr :> seq<string>
                let mutable isExist = Seq.exists ((=)nodeId) IDSeq 
                while isExist do
                    nodeId <- getId(index)
                    isExist <- Seq.exists ((=)nodeId) IDSeq
                IDArr <- Array.append IDArr [|nodeId|]
                let peerActor = spawn system nodeId (peer nodeId numN numR rowNum)
                // let peerActor = select ("akka://Pastry/user/" + nodeId) system
                peerArr <- Array.append peerArr [|peerActor|]
                index <- index + 1
                // join nodes
                peerActor <! Join jNodeId
        | Joined jdNodeId ->
            count <- count + 1
            if count = numNodes then
                // start routing
                printfn "Start routing"
                // bossMailbox.Self.Tell(Trigger true)
                for peerActor in peerArr do
                    let msg : string = "Pastry"
                    peerActor <! Route msg
            else
                bossMailbox.Self.Tell(InitJoin firstNodeId)
        | Trigger route ->
            trCount <- trCount + 1
            if count = numNodes && reqCount < numRequests then
                trCount <- 0
                for peerActor in peerArr do
                    let msg : string = "Pastry"
                    peerActor <! Route msg
                reqCount <- reqCount + 1
        | Finished numHops ->
            // printfn "%d" numHops
            tCount <- tCount + 1
            // printfn "total counts: %d" tCount
            totalHops <- totalHops + double(numHops)
            if tCount >= totalRequests then
                let avgHops : double = double(totalHops / double(totalRequests))
                printfn "Finished routing. The average hops: %.10f" avgHops
                // printfn "ID array: %A" IDArr
                Environment.Exit 1
                
        return! loop()
    }
    loop()

let main() = 
    let args = fsi.CommandLineArgs
    let numNodes : int = int args.[1]
    let numRequests : int = int args.[2]
    for timeout in [1000000] do
        try
            let pastryBoss = spawn system "pastryBoss" (pastryBoss numNodes numRequests)
            let task = (pastryBoss <? Init true)
            Async.RunSynchronously (task, timeout) 

        with :? IndexOutOfRangeException ->
            printfn "Wrong number of input arguments! Please refer to the following format:"
            printfn "dotnet fsi --langversion:preview project3.fsx [NUMBER OF NODES] [NUMBER OF REQUESTS]"

    0

main()
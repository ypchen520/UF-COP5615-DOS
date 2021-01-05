module Simulator
    open System
    open Messages
    open Util
    open Client
    open Akka.Actor
    open Akka.FSharp

    let selectRandMention(clients: byref<IActorRef []>, userNames: byref<string []>, task: string, num: int) =
        for i = 0 to num-1 do
            let randUserId = getRandNum(clients.Length)
            let randUserName = userNames.[randUserId] // TODO: create a map to store the mapping between user names and user IDs
            let mutable targetName = userNames.[getRandNum(userNames.Length)]
            while targetName = randUserName do
                targetName <- userNames.[getRandNum(userNames.Length)]
            clients.[randUserId] <! TweetMention (randUserId + 1, targetName)
            // printfn "client%A was selected to mention %A!" (randUserId + 1) targetName

    let selectRandSubscriber(clients: byref<IActorRef []>, userNames: byref<string []>, celebrity: byref<int []>, task: string, num: int) =
        let mutable targetId: int = getRandNum(userNames.Length)
        while isThere(celebrity, (targetId+1)) do // celebrity: 1-based
            targetId <- getRandNum(userNames.Length)
        let mutable targetName: string = userNames.[targetId]
        printfn "user%A is selected as the celebrity!" (targetId+1)
        celebrity <- Array.append celebrity [|(targetId+1)|] // celebrity: 1-based
        for i = 0 to num-1 do
            let mutable randUserId = getRandNum(clients.Length)
            while randUserId = targetId do
                randUserId <- getRandNum(clients.Length)
            let randUserName = userNames.[randUserId] // TODO: create a map to store the mapping between user names and user IDs
            clients.[randUserId] <! Subscribe (randUserId + 1, targetId + 1, targetName)
            // printfn "[selectRandSubscriber] Sanity check: %A and %A" (targetId + 1) targetName
            // printfn "client%A was selected to subscribe to %A!" (randUserId + 1) targetName
            
                
    let selectRandUser(clients: byref<IActorRef []>, task: string, num: int) =
        let mutable selected: int [] = Array.empty
        for i = 0 to num-1 do
            let mutable randUserId: int = getRandNum(clients.Length)
            while isThere(selected, randUserId) do
                randUserId <- getRandNum(clients.Length)
            selected <- Array.append selected [|randUserId|]
            // printfn "client%A was selected for %s!" (randUserId + 1) task
            match task with 
            | "TweetHashtag" ->
                clients.[randUserId] <! TweetHashtag (randUserId + 1)
            | "QueryHashtag" ->
                clients.[randUserId] <! QueryHashtag (randUserId + 1)
            | "QueryMentioned" ->
                clients.[randUserId] <! QueryMentioned (randUserId + 1)
            | "QuerySubscribed" -> 
                clients.[randUserId] <! QuerySubscribed (randUserId + 1)
            | "Retweet" -> 
                clients.[randUserId] <! Retweet (randUserId + 1)
            | _ ->
                printfn "[selectRandUser] Unknown pattern"

    let simulatorActor (simMailbox:Actor<SimulatorMsg>) = 
        // let selfActor = simMailbox.Self
        // let mutable servers = Array.empty
        let mutable clients: IActorRef [] = Array.empty
        let mutable sharedKeyClients: int [] = Array.empty
        let mutable userNames = Array.empty
        let mutable userIds = Array.empty
        let mutable numUser: int = 0
        // let mutable lastServerID: int = 0
        let mutable lastUserID: int = 0
        let mutable connected: int [] = Array.empty
        let mutable disconnected: int [] = Array.empty
        let mutable requestNum: int = 0
        let mutable wsNum: int = 0
        let mutable celebrity: int [] = Array.empty // 1-based
        let simTimer = System.Diagnostics.Stopwatch()
        let rec loop () = actor {    
            let! (msg: SimulatorMsg) = simMailbox.Receive()
            let sender = simMailbox.Sender()
            match msg with 
            | RegisterUser numU->
                requestNum <- requestNum + numU
                simTimer.Start() 
                let tClients = Array.init numU (fun index -> spawn system ("client" + string(index+lastUserID+1)) clientActor)
                numUser <- numUser + numU
                clients <- Array.append clients tClients
                for i = 0 to numU-1 do
                    let uid: int = lastUserID + i + 1
                    let userName: string = "user" + string(i+lastUserID+1)
                    userNames <- Array.append userNames [|userName|]
                    userIds <- Array.append userIds [|uid|]
                    clients.[i+lastUserID] <! Initialize uid
                    clients.[i+lastUserID] <! Register (uid, userName)
                    // clients.[i+lastUserID] <! FirstConnect uid
                    connected <- Array.append connected [|uid|]
                lastUserID <- numUser
            | ShareKey numU ->
                requestNum <- requestNum + numU
                let lastShared = sharedKeyClients.Length
                for i = 0 to numU-1 do
                    let id = lastShared+i+1
                    // printfn "id %A sharing" id
                    sharedKeyClients <- Array.append sharedKeyClients [|id|]
                    clients.[id-1] <! ShareSecretKey id
            | SimFirstConnect numU ->
                requestNum <- requestNum + numU
                for i = 0 to numU-1 do
                    clients.[i] <! FirstConnect (i+1)
            | SimConnect numU ->
                requestNum <- requestNum + numU
                for i = 0 to numU-1 do
                    let mutable uid: int = disconnected.[getRandNum(disconnected.Length)]
                    while isThere(connected, uid) do
                        uid <- disconnected.[getRandNum(disconnected.Length)]
                    printfn "user%A is selected to reconnect" uid 
                    // if not (isThere(connected, uid)) then
                    connected <- Array.append connected [|uid|]
                    clients.[uid-1] <! Connect uid
            | SimDisconnect numU ->
                requestNum <- requestNum + numU
                for i = 0 to numU-1 do
                    let mutable uid: int = connected.[getRandNum(connected.Length)]
                    while isThere(disconnected, uid) do
                        uid <- connected.[getRandNum(connected.Length)]
                    // if not (isThere(connected, uid)) then
                    connected <- Array.filter ((<>)uid) connected
                    disconnected <- Array.append disconnected [|uid|]
                    clients.[uid-1] <! Disconnect uid
            | SendTweets numU ->
                requestNum <- requestNum + numU
                // printfn "[Simulator] send tweet"
                for i = 0 to numU-1 do
                    // Tweet (i+1)
                    let uid: int = connected.[getRandNum(connected.Length)]
                    // printfn "%A %A" uid userNames.[uid-1]
                    clients.[uid-1] <! Tweet (uid, userNames.[uid-1])
            | SimSubscribe num ->
                requestNum <- requestNum + num
                selectRandSubscriber(&clients, &userNames, &celebrity, "Subscribe", num)
            | SimQuerySubscribed num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QuerySubscribed", num)    
            | SendTweetsHashtag num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "TweetHashtag", num)
            | SimQueryHashtag num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QueryHashtag", num)
            | SendTweetsMention num ->
                requestNum <- requestNum + num
                selectRandMention(&clients, &userNames, "TweetMention", num)
            | SimQueryMentioned num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QueryMentioned", num) 
            | SimRetweet num-> 
                requestNum <- requestNum + num
                selectRandUser(&clients, "Retweet", num) 
            | DebuggingInfo debugging ->
                if debugging = "true" then
                    for client in clients do
                        client <! PrintDebuggingInfo debugging
            | RequestDone reqDoneNum ->
                // printf "%A" reqDoneNum
                requestNum <- requestNum - reqDoneNum
                // printf "%A" requestNum
            | Done ->
                if requestNum > 0 then
                    printfn "Tasks are not done yet. Remaining request number: %A" requestNum   
                else if requestNum = -1000000 then
                    printfn "Finished tweeting ..."  
                    printfn "Press any key to terminate"
                    Console.ReadLine() |> ignore
                    Environment.Exit 1          
                else if requestNum = 0 then
                    requestNum <- -1000000
                    printfn "Finished tweeting. The time taken: %i" simTimer.ElapsedMilliseconds
                    printfn "Press any key to terminate"
                    Console.ReadLine() |> ignore
                    Environment.Exit 1
                    // for client in clients do
                    //     client <! DisconnectWS true
            // | DisconnectedWS -> 
            //     wsNum <- wsNum + 1
            //     if wsNum = clients.Length then
            //         printfn "All Done!"
            //         printfn "Press any key to terminate"
            //         Console.ReadLine() |> ignore
            //         Environment.Exit 1
            | _ ->
                printfn "[simulatorActor] Unknown pattern"
            return! loop ()
        } 
        loop ()
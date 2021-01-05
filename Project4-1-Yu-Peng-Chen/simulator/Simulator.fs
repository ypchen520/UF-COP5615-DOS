module Simulator
    open System
    open Client
    open TwitterClone
    open Akka.Actor
    open Akka.FSharp
    open FSharp.Data
    open FSharp.Data.JsonExtensions

    let selectRandMention(clients: byref<IActorRef []>, userNames: byref<string []>, task: string, num: int) =
        for i = 0 to num-1 do
            let randUserId = getRandNum(clients.Length)
            let randUserName = userNames.[randUserId] // TODO: create a map to store the mapping between user names and user IDs
            let mutable targetName = userNames.[getRandNum(userNames.Length)]
            while targetName = randUserName do
                targetName <- userNames.[getRandNum(userNames.Length)]
            let req = getReq(task, randUserId + 1, -1, targetName, [|("", "")|])
            // printfn "client%A was selected to mention %A!" (randUserId + 1) targetName
            clients.[randUserId] <! req

    let selectRandSubscriber(clients: byref<IActorRef []>, userNames: byref<string []>, celebrity: byref<int []>, task: string, num: int) =
        let mutable targetId: int = getRandNum(userNames.Length)
        while isThere(celebrity, (targetId+1)) do // celebrity: 1-based
            targetId <- getRandNum(userNames.Length)
        let mutable targetName: string = userNames.[targetId]
        celebrity <- Array.append celebrity [|(targetId+1)|] // celebrity: 1-based
        for i = 0 to num-1 do
            let mutable randUserId = getRandNum(clients.Length)
            while randUserId = targetId do
                randUserId <- getRandNum(clients.Length)
            let randUserName = userNames.[randUserId] // TODO: create a map to store the mapping between user names and user IDs
            let req = getReq(task, randUserId + 1, targetId + 1, targetName, [|("", "")|])
            // printfn "[selectRandSubscriber] Sanity check: %A and %A" (targetId + 1) targetName
            // printfn "client%A was selected to subscribe to %A!" (randUserId + 1) targetName
            clients.[randUserId] <! req
            
                
    let selectRandUser(clients: byref<IActorRef []>, task: string, num: int) =
        let mutable selected: int [] = Array.empty
        for i = 0 to num-1 do
            let mutable randUserId: int = getRandNum(clients.Length)
            while isThere(selected, randUserId) do
                randUserId <- getRandNum(clients.Length)
            selected <- Array.append selected [|randUserId|]
            // match task with 
            // | "TweetHashtag" ->
            //     printfn "client%A was selected for TweetHashtag!" (randUserId + 1)
            // | "QueryHashtag" ->
            //     printfn "client%A was selected for QueryHashtag!" (randUserId + 1)
            // | "QueryMentioned" ->
            //     printfn "client%A was selected for QueryMentioned!" (randUserId + 1)
            // | "QuerySubscribed" -> 
            //     printfn "client%A was selected for QuerySubscribed!" (randUserId + 1)
            // | "Retweet" -> 
            //     printfn "client%A was selected for Retweet!" (randUserId + 1)
            // | _ ->
            //     printfn "[selectRandUser] Unknown pattern"
            let req = getReq(task, randUserId + 1, -1, "", [|("", "")|])
            clients.[randUserId] <! req

    let simulatorActor (simMailbox:Actor<SimulatorMsg>) = 
        // let selfActor = simMailbox.Self
        // let mutable servers = Array.empty
        let mutable clients: IActorRef[] = Array.empty
        let mutable userNames = Array.empty
        let mutable userIds = Array.empty
        let mutable numUser: int = 0
        // let mutable lastServerID: int = 0
        let mutable lastUserID: int = 0
        let mutable connected: int [] = Array.empty
        let mutable requestNum: int = 0
        let mutable celebrity: int [] = Array.empty // 1-based
        let simTimer = new System.Diagnostics.Stopwatch()
        let rec loop () = actor {    
            let! (msg: SimulatorMsg) = simMailbox.Receive()
            let sender = simMailbox.Sender()
            match msg with 
            | RegisterUser numU->
                requestNum <- requestNum + numU
                simTimer.Start() 
                let tClients = Array.init numU (fun index -> spawn localSystem ("client" + string(index+lastUserID+1)) clientActor)
                numUser <- numUser + numU
                clients <- Array.append clients tClients
                for i = 0 to numU-1 do
                    let uid: int = lastUserID + i + 1
                    let userName: string = "user" + string(i+lastUserID+1)
                    userNames <- Array.append userNames [|userName|]
                    userIds <- Array.append userIds [|uid|]
                    let req1 = getReq("Initialize", uid, -1, "", [|("", "")|])
                    clients.[i+lastUserID] <! req1
                    let req2 = getReq("Register", uid, -1, userName, [|("", "")|])
                    clients.[i+lastUserID] <! req2
                    connected <- Array.append connected [|uid|]
                lastUserID <- numUser
            | Connect numU ->
                requestNum <- requestNum + numU
                for i = 0 to numU-1 do
                    let mutable uid: int = userIds.[getRandNum(userIds.Length)]
                    while isThere(connected, uid) do
                        uid <- userIds.[getRandNum(userIds.Length)]
                    // if not (isThere(connected, uid)) then
                    connected <- Array.append connected [|uid|]
                    let req = getReq("Connect", uid, -1, "", [|("", "")|])
                    clients.[uid-1] <! req
            | Disconnect numU ->
                requestNum <- requestNum + numU
                for i = 0 to numU-1 do
                    let mutable uid: int = userIds.[getRandNum(userIds.Length)]
                    while not (isThere(connected, uid)) do
                        uid <- userIds.[getRandNum(userIds.Length)]
                    // if not (isThere(connected, uid)) then
                    connected <- Array.filter ((<>)uid) connected
                    let req = getReq("Disconnect", uid, -1, "", [|("", "")|])
                    clients.[uid-1] <! req
            | SendTweets numU ->
                requestNum <- requestNum + numU
                // printfn "[Simulator] send tweet"
                for i = 0 to numU-1 do
                    // Tweet (i+1)
                    let req = getReq("Tweet", (i+1), -1, "", [|("", "")|])
                    clients.[i] <! req
            | SubscribeToOne num ->
                requestNum <- requestNum + num
                selectRandSubscriber(&clients, &userNames, &celebrity, "SubscribeToOne", num)
            | QuerySubscribed num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QuerySubscribed", num)    
            | SendTweetsHashtag num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "TweetHashtag", num)
            | QueryHashtag num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QueryHashtag", num)
            | SendTweetsMention num ->
                requestNum <- requestNum + num
                selectRandMention(&clients, &userNames, "TweetMention", num)
            | QueryMentioned num ->
                requestNum <- requestNum + num
                selectRandUser(&clients, "QueryMentioned", num) 
            | Retweet num-> 
                requestNum <- requestNum + num
                selectRandUser(&clients, "Retweet", num) 
            | DebuggingInfo debugging ->
                let req = getReq("PrintDebuggingInfo", -1, -1, debugging, [|("", "")|])
                if debugging = "true" then
                    for client in clients do
                        client <! req
            | RequestDone reqDoneNum ->
                // printf "%A" reqDoneNum
                requestNum <- requestNum - reqDoneNum
                // printf "%A" requestNum
            | Done ->
                if requestNum > 0 then
                    printfn "Tasks are not done yet. Remaining request number: %A" requestNum                
                else if requestNum = 0 then
                    printfn "Done"
                    printfn "Finished tweeting. The time taken: %i" simTimer.ElapsedMilliseconds
                    Environment.Exit 1
            | _ ->
                printfn "[simulatorActor] Unknown pattern"
            return! loop ()
        } 
        loop ()
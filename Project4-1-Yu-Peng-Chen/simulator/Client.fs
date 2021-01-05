module Client
    open System
    open System.IO
    open TwitterClone
    open FSharp.Data
    open FSharp.Data.JsonExtensions
    open FSharp.Json
    open Akka.Actor
    open Akka.FSharp

    let getRandomTweet(pattern: string) =
        let mutable tweet: string = ""
        match pattern with 
        | "plain" ->
            tweet <- plainTweetArr.[getRandNum(plainTweetArr.Length)]
        | "hashtag" ->
            tweet <- taggedTweetArr.[getRandNum(taggedTweetArr.Length)]
        | _ ->
            printfn("[getRandomTweet] Unknown pattern")
        tweet

    let saveToQueriedTweets(qTweets: byref<(string * string)[]>, tweets: (string * string)[]) = 
        qTweets <- Array.append qTweets tweets

    let saveToFile(log: byref<string>, logFoldername: string, uid: int) =
        let dir = Environment.CurrentDirectory
        let subpath = Path.Combine(dir, logFoldername)
        if not (Directory.Exists subpath) then
            printfn "directory not found: %A" subpath
        else    
            let filename = "log"+string(uid)+".txt"
            let path = Path.Combine(subpath, filename)
            // if not (File.Exists(path)) then
            File.CreateText(path) |> ignore
            File.WriteAllText(path, log)

    let storeToLog(pattern: string, uid: int, tweets: (string * string)[], log: byref<string>) =
        // storeToLog()
        // let mutable tempLog: string = "["+pattern+"]"+"\n"
        let mutable tempLog: string = ""
        if tweets.Length = 0 then
            match pattern with
            | "QueryHashtagBack" ->
                tempLog <- tempLog + "Hashtag not found!\n"
            | "QueryMentionedBack" ->
                tempLog <- tempLog + "User not mentioned yet!\n"
            | "QuerySubscribedBack" ->
                tempLog <- tempLog + "Has not subscribed to anyone yet!\n"
            | _ ->
                printfn("[storeToLog] Unknown pattern")
        else
            for tweet in tweets do
                let twitter: string = fst tweet
                let content: string = snd tweet
                let temp: string = "<"+twitter+"> " + content + "\n"
                tempLog <- tempLog + temp
        log <- log + tempLog
        // printfn("[client%A] %A: %A") uid pattern tweets
        
    let clientActor (clientMailbox:Actor<String>) = 
        let selfActor = clientMailbox.Self
        let mutable userID: int = -1
        let mutable isConnected: bool = false
        // let mutable hTweets: (string * string)[] = Array.empty
        // let mutable mTweets: (string * string)[] = Array.empty
        // let mutable sTweets: (string * string)[] = Array.empty
        let mutable qTweets: (string * string)[] = Array.empty
        let mutable subscribedTo: (int * string)[] = Array.empty
        let mutable log: string = ""
        let mutable processes: int = 0
        // let mutable usedHashtags: string [] = Array.empty
        let rec loop () = actor {    
            let! message = clientMailbox.Receive()
            let sender = clientMailbox.Sender()
            let info = Json.deserialize<RecordType> message
            let request = info.req
            let uid = info.uid
            log <- log + "[" + request + "]\n"
            match request with 
            | "Initialize" ->
                userID <- uid
            | "Register" ->
                let uName = info.content
                let req = getReq("RegisterAccount", uid, -1, uName, [|("", "")|])
                remoteServer <! req
                isConnected <- true
                // log <- log + "[" + request + "]\n"
                log <- log + "[" + "Connect" + "]\n"
            | "RegisterBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "Connect" ->
                isConnected <- true
                // log <- log + "[" + request + "]\n"
                let req = getReq("Connect", uid, -1, "", [|("", "")|])
                remoteServer <! req
                // sender <! RequestDone 1
            | "ConnectBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "Disconnect" ->
                isConnected <- false
                let req = getReq("Disconnect", uid, -1, "", [|("", "")|])
                remoteServer <! req
                // sender <! RequestDone 1
            | "DisconnectBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "Tweet" ->
                let tweet: string = getRandomTweet("plain")
                let req = getReq("ReceiveTweets", uid, -1, tweet, [|("", "")|])
                // log <- log + "Client" + string(uid) + " tweeted!\n"
                remoteServer <! req
                // sender <! RequestDone 1
            | "TweetBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "TweetHashtag" ->
                let tweet: string = getRandomTweet("hashtag")
                let req = getReq("ReceiveTweets", uid, -1, tweet, [|("", "")|])
                // log <- log + "Client" + string(uid) + " tweeted hashtags!\n"                
                remoteServer <! req
                // sender <! RequestDone 1
            // | "TweetHashtagBack" ->
            //     let simAct = selectActor("simulator", "")
            //     simAct <! RequestDone 1
            | "TweetMention" ->
                let targetName = info.content
                let tweet: string = getMentionTweet(targetName)
                log <- log + "Mentioning: " + targetName + "\n"
                let req = TwitterClone.getReq("ReceiveTweets", uid, -1, tweet, [|("", "")|])
                remoteServer <! req
                // sender <! RequestDone 1
            // | "TweetMentionBack" ->
            //     let simAct = selectActor("simulator", "")
            //     simAct <! RequestDone 1
            | "SubscribeToOne" ->
                let targetId = info.target
                let targetName = info.content
                let req = getReq("Subscribe", uid, targetId, targetName, [|("", "")|])
                log <- log + "Subscribing to " + targetName + "\n"
                subscribedTo <- Array.append subscribedTo [|targetId, targetName|]
                remoteServer <! req
                // sender <! RequestDone 1
            | "SubscribeToOneBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "QueryHashtag" ->
                // printfn "%A" usedHashtags
                let tag = hashtagsArr.[getRandNum(hashtagsArr.Length)]
                log <- log + tag + "\n"
                let req = TwitterClone.getReq("QueryHashtag", uid, -1, tag, [|("", "")|])
                remoteServer <! req
            | "QueryHashtagBack" ->
                if userID <> uid then
                    printfn "[client%A] user%A does not exist!" userID uid
                // let response = info.content
                let tweets = info.multipleContent
                storeToLog("QueryHashtagBack", uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets) 
                // if response = "Hashtag not found!" then
                //     printfn("[client%A] QueryHashtagBack: %A") uid response
                // else
                //     saveToQueriedTweets(&qTweets, tweets)
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1  
            | "QueryMentioned" -> 
                if userID <> uid then
                    printfn "[client%A] user%A does not exist!" userID uid
                // printfn "[client%A] QueryMentioned" uid
                let req = getReq("QueryMentioned", uid, -1, "", [|("", "")|])
                remoteServer <! req
            | "QueryMentionedBack" ->
                let tweets = info.multipleContent
                storeToLog("QueryMentionedBack", uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets)
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1  
            | "LiveMentioned" ->
                let tweets = info.multipleContent
                storeToLog("LiveMentioned", uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets) 
            | "QuerySubscribed" ->
                if subscribedTo.Length <> 0 then
                    let queryTarget: (int * string) = subscribedTo.[getRandNum(subscribedTo.Length)]
                    let targetId: int = fst queryTarget
                    let targetName: string = snd queryTarget
                    let req = getReq("QuerySubscribed", uid, targetId, targetName, [|("", "")|])
                    remoteServer <! req
                    log <- log + targetName + "\n"
                else
                    log <- log + "client" + string(uid) + " has not subscribed to any users yet.\n"
                    sender <! RequestDone 1
            | "QuerySubscribedBack" ->
                let tweets = info.multipleContent
                storeToLog("QuerySubscribedBack", uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets)
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1  
            | "LiveSubscribed" -> 
                let tweets = info.multipleContent
                storeToLog("LiveSubscribed", uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets) 
            | "Retweet" -> 
                if qTweets.Length <> 0 then
                    let retweet: (string * string) = qTweets.[getRandNum(qTweets.Length)]
                    let author: string = fst retweet
                    log <- log + "retweeted " + author + "'s tweet.\n"
                    let req = getReq("ReceiveReTweets", uid, -1, "", [|retweet|])
                    remoteServer <! req 
                else
                    log <- log + "client" + string(uid) + " has not queried any tweets yet.\n"
                    sender <! RequestDone 1
            | "RetweetBack" ->
                let simAct = selectActor("simulator", "")
                simAct <! RequestDone 1
            | "PrintDebuggingInfo" ->
                let printOrNot: string = info.content
                if printOrNot = "true" then
                    saveToFile(&log, logFoldername, userID)
            | _ ->
                printfn "[clientActor] Unknown pattern"
            return! loop ()
        } 
        loop ()
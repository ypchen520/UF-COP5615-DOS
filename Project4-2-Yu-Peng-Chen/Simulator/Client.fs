module Client
    open System
    open System.IO
    open System.Security.Cryptography
    open Messages
    open Util
    open FSharp.Json
    open Akka.Actor
    open Akka.FSharp
    open WebSocketSharp.Server
    open WebSocketSharp

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
            | "QueryHashtagRes" ->
                tempLog <- tempLog + "Hashtag not found!\n"
            | "QueryMentionedRes" ->
                tempLog <- tempLog + "User not mentioned yet!\n"
            | "QuerySubscribedRes" ->
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
        
    let clientActor (clientMailbox:Actor<ClientMsg>) = 
        let selfActor = clientMailbox.Self
        let simAct = selectActor("simulator", "")
        // let privateKey
        let wsReg = new WebSocket("ws://localhost:8080/register")
        let wsTweet = new WebSocket("ws://localhost:8080/receivetw")
        let wsRetweet = new WebSocket("ws://localhost:8080/receiveretw")
        let wsSubscribe = new WebSocket("ws://localhost:8080/subscribe")
        let wsQuery = new WebSocket("ws://localhost:8080/query")
        let wsConnect = new WebSocket("ws://localhost:8080/connect")
        let wsShareKey = new WebSocket("ws://localhost:8080/sharekey")
        let mutable userID: int = -1
        let mutable isConnected: bool = false
        let mutable qTweets: (string * string)[] = Array.empty
        let mutable subscribedTo: (int * string)[] = Array.empty
        let mutable log: string = ""
        let mutable isClosing: bool = false
        // let mutable processes: int = 0
        (*RSA for connect authentication*)
        let rsa: RSA = RSA.Create()
        let rsaParam = rsa.ExportParameters(false)
        let rsaFormatter: RSAPKCS1SignatureFormatter = RSAPKCS1SignatureFormatter(rsa) (*use private key to sign*)
        (*ECDH for shared secret and digital signature*)
        let clientECDH = ECDiffieHellman.Create()
        let mutable sharedSecretKey: byte [] = Array.empty
        (*Connect to WS*)
        wsConnect.Connect()
        wsReg.Connect()
        wsTweet.Connect()
        wsSubscribe.Connect()
        wsQuery.Connect()
        wsRetweet.Connect()
        wsShareKey.Connect()
        wsConnect.Log.Level <- LogLevel.Fatal
        wsReg.Log.Level <- LogLevel.Fatal
        wsTweet.Log.Level <- LogLevel.Fatal
        wsSubscribe.Log.Level <- LogLevel.Fatal
        wsQuery.Log.Level <- LogLevel.Fatal
        wsRetweet.Log.Level <- LogLevel.Fatal
        wsShareKey.Log.Level <- LogLevel.Fatal
        (*onError*)
        wsConnect.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsReg.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsTweet.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsSubscribe.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsQuery.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsRetweet.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        wsShareKey.OnError.Add(fun e -> 
             printfn "[user%A] Server is busy! Please try again later" userID
             simAct <! RequestDone 1
        )
        (*onMessage*)
        wsReg.OnMessage.Add(fun e ->
            let info = Json.deserialize<ConfirmResponse> e.Data
            simAct <! RequestDone 1
            // printfn "user%d registered" info.UserID
        )
        wsShareKey.OnMessage.Add(fun e->
            let info = Json.deserialize<SharedKeyInfo> e.Data
            let tempECDH = ECDiffieHellman.Create()
            let serverPublicKey = info.PublicKey |> Base64StrToBytes
            let publicKeySize = clientECDH.KeySize
            let publicKey: ReadOnlySpan<byte> = ReadOnlySpan<byte>(serverPublicKey)
            tempECDH.ImportSubjectPublicKeyInfo(publicKey, ref publicKeySize) |> ignore
            sharedSecretKey <- clientECDH.DeriveKeyMaterial(tempECDH.PublicKey)
            // printfn "%A's key: %A" info.UserID sharedSecretKey
            // let simAct = selectActor("simulator", "")
            simAct <! RequestDone 1
        )
        wsTweet.OnMessage.Add(fun e ->
            // Tweet, TweetHashtag, TweetMention
            // let simAct = selectActor("simulator", "")
            simAct <! RequestDone 1
            // wsTweet.Close()
        )
        wsRetweet.OnMessage.Add(fun e ->
            // let simAct = selectActor("simulator", "")
            simAct <! RequestDone 1
            // wsRetweet.Close()
        )
        wsSubscribe.OnMessage.Add(fun e ->
            // let simAct = selectActor("simulator", "")
            simAct <! RequestDone 1
        )
        wsQuery.OnMessage.Add(fun e ->
            // QueryHashtagRes, QueryMentionedRes, QuerySubscribedRes
            let info = Json.deserialize<TweetResponse> e.Data
            let req = info.ReqType
            let uid = info.UserID
            let tweets = info.Tweets
            log <- log + "[" + req + "]\n"
            storeToLog(req, uid, tweets, &log)
            saveToQueriedTweets(&qTweets, tweets)
            // printfn "user%A receives queried tweets: %A" uid tweets 
            // printfn "user%A's qTweets: %A" uid qTweets
            // let simAct = selectActor("simulator", "")
            simAct <! RequestDone 1
        )
        wsConnect.OnMessage.Add(fun e ->
            // let simAct = selectActor("simulator", "")
            let info = Json.deserialize<TweetResponse> e.Data
            let req = info.ReqType
            let uid = info.UserID            
            match req with
            | "ConnectChallenge" -> 
                let challenge = info.TargetContent
                rsaFormatter.SetHashAlgorithm("SHA256")
                let hashedChallenge = 
                    challenge 
                    |> Base64StrToBytes 
                    |> timePadding 
                    |> hashSHA256
                let signedHashValue = rsaFormatter.CreateSignature(hashedChallenge);
                let strSignature = signedHashValue |> bytesToBase64Str 
                let strHashedChallenge = hashedChallenge |> bytesToBase64Str
                let req = getConnectRequest("ReplyChallenge", info.UserID, strHashedChallenge, strSignature, "SHA256")
                wsConnect.Send(req)
            | "Connect" ->
                if info.TargetContent = "OK" then
                    ()
                    // printfn "user%d connected" info.UserID
                else if info.TargetContent = "FAIL" then
                    printfn "user%d failed to connect! Please try again later (Invalid private key)" info.UserID
                    // wsConnect.Close() (*the server side should not keep the session*)
                simAct <! RequestDone 1
            | "Disconnect" ->
                printfn "user%d disconnected" info.UserID
                // wsReg.Close()
                // wsConnect.Close()
                // wsTweet.Close()
                // wsSubscribe.Close()
                // wsQuery.Close()
                // wsRetweet.Close()
                // wsShareKey.Close()
                simAct <! RequestDone 1
            | "LiveMentioned" | "LiveSubscribed" -> 
                let tweets = info.Tweets
                // printfn "user%A receives live tweets: %A" uid tweets
                storeToLog(req, uid, tweets, &log)
                saveToQueriedTweets(&qTweets, tweets)
            | _ -> 
                printfn "unknown pattern"
        )
        let rec loop () = actor {    
            let! message = clientMailbox.Receive()
            let sender = clientMailbox.Sender()
            match message with 
            | Initialize uid->
                log <- log + "[Initialize]\n"
                userID <- uid
            | Register (uid, uName) ->
                log <- log + "[Register]\n"
                log <- log + "[Connect]\n"
                let strM = rsaParam.Modulus |> bytesToBase64Str
                let strE = rsaParam.Exponent |> bytesToBase64Str
                let publicKeyInfo = (strM, strE)
                let registerMsg = getRegisterRequest(uid, uName, publicKeyInfo)
                wsReg.Send(registerMsg)
                // selfActor <! FirstConnect uid
                // isConnected <- true
            | FirstConnect uid ->
                log <- log + "[FirstConnect]\n" 
                isConnected <- true
                let connectMsg = getConnectRequest("FirstConnect", uid, "", "","")
                // printfn "user%A connecting for the first time" uid
                // if not (wsConnect.IsAlive) then
                //     wsConnect.Connect()
                // if not wsTweet.IsAlive then
                //     wsTweet.Connect()
                // if not wsSubscribe.IsAlive then
                //     wsSubscribe.Connect()
                // if not wsQuery.IsAlive then
                //     wsQuery.Connect()
                // if not wsRetweet.IsAlive then
                //     wsRetweet.Connect()
                // if not wsShareKey.IsAlive then
                //     wsShareKey.Connect()             
                wsConnect.Send(connectMsg)   
            | Connect uid ->
                log <- log + "[Connect]\n"
                isConnected <- true
                let connectMsg = getConnectRequest("Connect", uid, "", "", "")
                // if not wsConnect.IsAlive then
                //     wsConnect.Connect()
                // if not wsTweet.IsAlive then
                //     wsTweet.Connect()
                // if not wsSubscribe.IsAlive then
                //     wsSubscribe.Connect()
                // if not wsQuery.IsAlive then
                //     wsQuery.Connect()
                // if not wsRetweet.IsAlive then
                //     wsRetweet.Connect()
                // if not wsShareKey.IsAlive then
                //     wsShareKey.Connect()             
                wsConnect.Send(connectMsg)
            | Disconnect uid ->
                log <- log + "[Disconnect]\n"
                isConnected <- false
                let disconnectMsg = getConnectRequest("Disconnect", uid, "", "", "")
                wsConnect.Send(disconnectMsg)
            | ShareSecretKey uid ->
                let clientPublicKey: string = clientECDH.ExportSubjectPublicKeyInfo() |> bytesToBase64Str
                let shareKeyMessage = getSharedKeyRequest(uid, clientPublicKey)
                wsShareKey.Send(shareKeyMessage)
            | Tweet (uid, uName) ->
                log <- log + "[Tweet(plain)]\n"
                let tweet: string = getRandomTweet("plain")
                let mutable isSigned: bool = false
                if sharedSecretKey.Length <> 0 then
                    isSigned <- true
                let tweetMsg = getTweetsRequest(uid, uName, tweet, "", isSigned)
                (*sign message*)
                let digitalSignature = getHMACSignature(tweetMsg, sharedSecretKey) |> bytesToBase64Str
                let signedMessage = getSignedMessageRequest(uid, tweetMsg, digitalSignature)
                wsTweet.Send(signedMessage)
            | TweetHashtag uid ->
                log <- log + "[Tweet(Hashtag)]\n"
                let tweet: string = getRandomTweet("hashtag")
                let mutable isSigned: bool = false
                if sharedSecretKey.Length <> 0 then
                    isSigned <- true
                let tweetMsg = getTweetsRequest(uid, "", tweet, "", isSigned)
                (*sign message*)
                let digitalSignature = getHMACSignature(tweetMsg, sharedSecretKey) |> bytesToBase64Str
                let signedMessage = getSignedMessageRequest(uid, tweetMsg, digitalSignature)
                wsTweet.Send(signedMessage)         
            | TweetMention (uid, targetName) ->
                log <- log + "[Tweet(Mention)]\n"                
                log <- log + "Mentioning: " + targetName + "\n"
                let tweet: string = getMentionTweet(targetName)
                let mutable isSigned: bool = false
                if sharedSecretKey.Length <> 0 then
                    isSigned <- true
                let tweetMsg = getTweetsRequest(uid, "", tweet, targetName, isSigned)  
                (*sign message*)
                let digitalSignature = getHMACSignature(tweetMsg, sharedSecretKey) |> bytesToBase64Str
                let signedMessage = getSignedMessageRequest(uid, tweetMsg, digitalSignature)
                wsTweet.Send(signedMessage)
            | Subscribe (uid, targetId, targetName) ->
                log <- log + "[Subscribe]\n" 
                log <- log + "Subscribing to " + targetName + "\n"
                subscribedTo <- Array.append subscribedTo [|targetId, targetName|]
                let subscribeMsg = getSubscribeRequest(uid, targetId)
                wsSubscribe.Send(subscribeMsg)
            | QueryHashtag uid ->
                log <- log + "[Query(Hashtag)]\n"
                let tag = hashtagsArr.[getRandNum(hashtagsArr.Length)]
                log <- log + tag + "\n"
                let queryMsg = getQueryRequest("QueryHashtag", uid, -1, tag)
                wsQuery.Send(queryMsg)
            | QueryMentioned uid-> 
                log <- log + "[Query(Mentioned)]\n"
                let queryMsg = getQueryRequest("QueryMentioned", uid, -1, "")
                wsQuery.Send(queryMsg) 
            | QuerySubscribed uid->
                log <- log + "[Query(Subscribed)]\n"
                if subscribedTo.Length <> 0 then
                    let queryTarget: (int * string) = subscribedTo.[getRandNum(subscribedTo.Length)]
                    let targetId: int = fst queryTarget
                    let targetName: string = snd queryTarget
                    let queryMsg = getQueryRequest("QuerySubscribed", uid, targetId, targetName)
                    log <- log + targetName + "\n"
                    wsQuery.Send(queryMsg)
                else
                    log <- log + "client" + string(uid) + " has not subscribed to any users yet.\n"
                    sender <! RequestDone 1
            | Retweet uid -> 
                log <- log + "[Retweet]\n"
                if qTweets.Length <> 0 then
                    // let retweet: (string * string) = qTweets.[getRandNum(qTweets.Length)]
                    let retweet:string = snd qTweets.[getRandNum(qTweets.Length)]
                    let author: string = fst qTweets.[getRandNum(qTweets.Length)]
                    let mutable isSigned: bool = false
                    if sharedSecretKey.Length <> 0 then
                        isSigned <- true
                    let retweetMsg = getTweetsRequest(uid, "", retweet, author, isSigned) //TODO: signed or not
                    log <- log + "retweeted " + author + "'s tweet.\n"
                    (*sign message*) 
                    let digitalSignature = getHMACSignature(retweetMsg, sharedSecretKey) |> bytesToBase64Str
                    let signedMessage = getSignedMessageRequest(uid, retweetMsg, digitalSignature)
                    wsRetweet.Send(signedMessage)
                else
                    log <- log + "client" + string(uid) + " has not queried any tweets yet.\n"
                    sender <! RequestDone 1
            | PrintDebuggingInfo isPrint ->
                let printOrNot: string = isPrint
                if printOrNot = "true" then
                    saveToFile(&log, logFoldername, userID)
            // | DisconnectWS disconnect ->
            //     // let registerMsg = getRegisterRequest(userID, "close", ("",""))
            //     // wsReg.Send(registerMsg)
            //     // try
            //     //     wsReg.Close()
            //     // with :? System.ObjectDisposedException ->
            //     //     printfn "client has disconnected!"
            //     // wsConnect.Close()
            //     // wsTweet.Close()
            //     // wsSubscribe.Close()
            //     // wsQuery.Close()
            //     // wsRetweet.Close()
            //     // wsShareKey.Close()
            //     if wsReg.IsAlive then
            //         wsReg.Close()
            //     if wsConnect.IsAlive then
            //         wsConnect.Close()
            //     if wsTweet.IsAlive then
            //         wsTweet.Close()
            //     if wsSubscribe.IsAlive then
            //         wsSubscribe.Close()
            //     if wsQuery.IsAlive then
            //         wsQuery.Close()
            //     if wsRetweet.IsAlive then
            //         wsRetweet.Close()
            //     if wsShareKey.IsAlive then
            //         wsShareKey.Close()
            //     sender <! DisconnectedWS
            return! loop ()
        } 
        loop ()
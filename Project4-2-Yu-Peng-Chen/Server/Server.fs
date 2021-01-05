module Server
    open System
    open System.Collections.Generic
    open System.Security.Cryptography
    open WebSocketSharp.Server
    open WebSocketSharp
    open Messages
    open Util
    open Database
    open Akka.Actor
    open Akka.FSharp
    open FSharp.Json
    
    let querySubscribed(userNames: byref<Map<string, int>>, targetName: string, userId: int, 
                        session: WebSocketSessionManager, sid: string) = 
        if targetName.Length <> 0 then
            let targetId: int = userNames.[targetName]
            let dbActor = selectActor("db", string targetId)
            dbActor <! SendTweets (userId, session, sid)  //(userId, session, sid): the user requested for the tweets; targetId: the user who's subscribed to
        else
            let dbActor = selectActor("db", string userId)
            dbActor <! LookUpSubscribed (userId, session, sid)
            
    let replyToQueryHashtag(hashtags: Dictionary<string, (string * string)[]>, hashtag: string, 
                            uid: int, session: WebSocketSessionManager, sid: string) = 
        if hashtags.ContainsKey(hashtag) then
            let tweets: (string * string)[] = hashtags.[hashtag]
            let batch: int = int(ceil(double(tweets.Length)/double(10))) //batch size = 10
            let mutable startIdx: int = 0
            let mutable endIdx: int = 0
            for i = 1 to batch do
                if i <> batch then
                    startIdx <- (i-1) * 10
                    endIdx <- i * 10 - 1
                    let partOfTweets = tweets.[startIdx..endIdx]
                    let res = getTweetsReponse("QueryHashtagRes", uid, partOfTweets, -1, "", false)
                    session.SendTo(res, sid)
                else
                    let partOfTweets = tweets.[(endIdx+1)..]
                    let res = getTweetsReponse("QueryHashtagRes", uid, partOfTweets, -1, "", false)
                    session.SendTo(res, sid)            
        else 
            let res = getTweetsReponse("QueryHashtagRes", uid, [||], -1, "", false) //The client side will show "Hashtag not found!"
            session.SendTo(res, sid)            

    let extractNameOrTag(tweet: string, pattern: char) = 
        let mutable subtweet: string = tweet
        let mutable index: int = tweet.IndexOf(pattern)
        let mutable targets: string [] = Array.empty
        while index <> -1 do
            subtweet <- subtweet.[index..]
            let nextSpaceIdx = subtweet.IndexOf(" ")
            let target: string = subtweet.[..(nextSpaceIdx-1)]
            if not (isThere(targets, target)) then
                targets <- Array.append targets [|target|]
            subtweet <- subtweet.[(nextSpaceIdx+1)..]
            index <- subtweet.IndexOf(pattern)
        targets

    let processHashtagOrMentions(liveSessions: Dictionary<int, (WebSocketSessionManager * string)>,
                                 hashtags: Dictionary<string, (string * string)[]>, 
                                 userNames: byref<Map<string, int>>, userIDs: byref<Map<int, string>>, 
                                 connected: byref<int []>, uid: int, tweet: string
                                ) = 
        let mutable uNames: string[] = Array.empty
        let mutable tags: string[] = Array.empty
        if Util.checkForHashtagOrMention(tweet, '@') then
            uNames <- extractNameOrTag(tweet, '@')
        if Util.checkForHashtagOrMention(tweet, '#') then
            tags <- extractNameOrTag(tweet, '#')

        for uName in uNames do
            let uNameWithoutSymbol: string = uName.[1..]
            // printfn "[Server] mentioned: %A" uNameWithoutSymbol
            let id: int = userNames.[uNameWithoutSymbol] //id of the mentioned user
            // let dbAct = select ("akka://Util/user/db" + string id) system
            let dbAct = selectActor("db", string id)
            dbAct <! StoreMentions (userIDs.[uid], tweet) //uid: id of the user who mentions other people
            if isThere(connected, id) then 
                let res = getTweetsReponse("LiveMentioned", uid, [|(userIDs.[uid], tweet)|], id, "", false)
                try
                    let mentionedUserSession = fst liveSessions.[id]
                    let mentionedUserSessionID = snd liveSessions.[id]
                    mentionedUserSession.SendTo(res, mentionedUserSessionID)
                with :? WebSocketSharp.WebSocketException ->
                    printfn "client%d has already disconnected!" id
        
        for tag in tags do
            if not (hashtags.ContainsKey(tag)) then
                hashtags.Add(tag, [|(userIDs.[uid], tweet)|])
            else if hashtags.ContainsKey(tag) then
                hashtags.[tag] <- Array.append hashtags.[tag] [|(userIDs.[uid], tweet)|]         
                
    let subscribe(uid: int, targetId: int) =             
        // printfn "[Server] %A subscribed to: %A" uid targetId
        let dbActor = selectActor("db", string uid)
        dbActor <! AddToFollowing targetId

    let saveTweetsToDB(uid: int, tweet: string) = 
        let dbActor = selectActor("db", string uid)
        dbActor <! StoreTweets (uid, tweet)
    
    let saveReTweetsToDB(uid: int, tweet: string, targetName: string) = 
        // targetName: the author of the tweet that is retweeted
        let dbActor = selectActor("db", string uid)
        dbActor <! StoreReTweets (uid, tweet, targetName)

    let registerUser(users: byref<int []>, 
                     connected: byref<int []>,
                     userNames: byref<Map<string, int>>, 
                     userIDs: byref<Map<int, string>>, 
                     uid: int, uName: string) =
        users <- Array.append users [|uid|]
        // connected <- Array.append connected [|uid|]
        // userNames <- Array.append userNames [|uName|]
        userNames <- userNames.Add(uName, uid)
        userIDs <- userIDs.Add(uid, uName)
        let dbActor = spawn system ("db" + string(uid)) dbActor
        // printfn "db%d spawned" uid
        // dbActors <- Array.append dbActors [|dbActor|]
        dbActor <! Init (uid, uName)
        // printfn "[Server] currently registered: %A" userNames
    
    let sendToConnected(liveSessions: Dictionary<int, (WebSocketSessionManager * string)>,
                        connected: byref<int []>, 
                        follower: int, followed: int, 
                        tweet: (string * string)) =
        if isThere(connected, follower) then 
            let res = getTweetsReponse("LiveSubscribed", followed, [|tweet|], follower, "", false)
            printfn "user%A is sending a live tweet to user%A" followed follower
            try
                let followerSession = fst liveSessions.[follower]
                let followerUserSessionID = snd liveSessions.[follower]
                followerSession.SendTo(res, followerUserSessionID)
            with :? WebSocketSharp.WebSocketException ->
                printfn "client%d has already disconnected!" follower
    
    let verifyDigitalSignature(publicKeys: Dictionary<int, (string * string)>, 
                               uid: int,
                               bHashedMsg: byte [],
                               bDigiSignature:byte [],
                               hashAlgo: string) = 
        let publicKey = publicKeys.[uid]
        let mutable rsaKeyInfo: RSAParameters = RSAParameters()
        rsaKeyInfo.Modulus <- publicKey |> fst |> Base64StrToBytes
        rsaKeyInfo.Exponent <- publicKey |> snd |> Base64StrToBytes
        (*import client's public key*)
        let rsa: RSA = RSA.Create()    
        rsa.ImportParameters(rsaKeyInfo)
        let rsaDeformatter: RSAPKCS1SignatureDeformatter = RSAPKCS1SignatureDeformatter(rsa)
        rsaDeformatter.SetHashAlgorithm(hashAlgo)
        let isVerified: bool = rsaDeformatter.VerifySignature(bHashedMsg, bDigiSignature)
        isVerified

    let serverActor (serverMailbox:Actor<ServerMsg>) = 
        let selfActor = serverMailbox.Self
        let mutable users: int [] = Array.empty
        let mutable userNames: Map<string, int> = Map.empty
        let mutable userIDs: Map<int, string> = Map.empty
        let hashtags = new Dictionary<string, (string * string)[]>()
        let mutable deleted: string [] = Array.empty
        let mutable connected: int [] = Array.empty
        let mutable disconnected: int [] = Array.empty
        let liveSessions = new Dictionary<int, (WebSocketSessionManager * string)>()
        let publicKeys = new Dictionary<int, (string * string)>() // TODO
        (*Digital Signature ECDH*)
        let serverECDH = ECDiffieHellman.Create()
        let serverPublicKey: string = serverECDH.ExportSubjectPublicKeyInfo() |> bytesToBase64Str
        let sharedSecretKeys = new Dictionary<int, byte []>()
        let rec loop () = actor {    
            let! (msg: ServerMsg) = serverMailbox.Receive()
            match msg with 
            | RegisterAccount (uid, uName, publicKey, session, sid) ->
                registerUser(&users, &connected, &userNames, &userIDs, uid, uName)
                if not (publicKeys.ContainsKey(uid)) then
                    publicKeys.Add(uid, publicKey) // TODO
                reply("Register", uid, session, sid)
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A registered!" uid
                | _ ->
                    ()
            | ShareSecretKey (uid, clientPublicKey, session, sid) ->
                let tempECDH = ECDiffieHellman.Create()
                let publicKey = clientPublicKey |> Base64StrToBytes
                let keySize = serverECDH.KeySize
                let pub: ReadOnlySpan<byte> = ReadOnlySpan<byte>(publicKey)
                tempECDH.ImportSubjectPublicKeyInfo(pub, ref keySize) |> ignore
                let sharedSecretKey = serverECDH.DeriveKeyMaterial(tempECDH.PublicKey)
                if not (sharedSecretKeys.ContainsKey(uid)) then
                    sharedSecretKeys.Add(uid, sharedSecretKey)
                let res = getSharedKeyResponse(uid, serverPublicKey)
                session.SendTo(res, sid)
                match LOGLEVEL with
                | "trace" | "tweets" | "security" ->
                    printfn "user%A shared a secret key with the server!" uid
                | _ ->
                    ()
            | VerifyMessage (reqType, uid, jsonMessage, digitalSignature, session, sid) ->
                let mutable signatureVerified: bool = false
                let mutable sharedKey: byte [] = Array.empty
                let mutable isVerified: bool = false
                if sharedSecretKeys.ContainsKey(uid) then
                    sharedKey <- sharedSecretKeys.[uid]
                    // printfn "user%A sharedKey: %A" uid sharedKey
                    let digiSign = digitalSignature |> Base64StrToBytes
                    isVerified <- verifyHMACSignature(jsonMessage, digiSign, sharedKey)
                match LOGLEVEL with
                | "trace" | "tweets" | "security" ->
                    printfn "user%A's message verified: %A" uid isVerified
                | _ ->
                    ()
                if isVerified then
                    signatureVerified <- true
                let info = Json.deserialize<TweetInfo> jsonMessage 
                match reqType with
                | "ReceiveTW" -> 
                    selfActor <! ReceiveTweets (info.UserID, info.Tweet, signatureVerified, session, sid)
                | "ReceiveReTW" ->
                    selfActor <! ReceiveReTweets (info.UserID, info.Tweet, signatureVerified, info.TargetName, session, sid)
                | _ ->
                    printfn "[VerifyMessage] unknown pattern"
            | Connected (uid, session, sid) ->
                if not (isThere(connected, uid)) then
                    connected <- Array.append connected [|uid|]
                    liveSessions.Add(uid, (session, sid))
                let res = getTweetsReponse("Connect", uid, [||], -1, "OK", false)
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A is connected" uid
                | _ ->
                    ()
                session.SendTo(res, sid)
                // reply("connect", uid, session, sid)
            | SendChallenge (uid, session, sid) ->
                let challenge: string = genChallenge()
                match LOGLEVEL with
                | "trace" | "tweets" | "security" ->
                    printfn "user%A is re-connecting ..." uid
                | _ ->
                    ()
                let res = getTweetsReponse("ConnectChallenge", uid, [||], -1, challenge, false)
                session.SendTo(res, sid)
            | VerifyChallenge (uid, hashedMsg, dSignature, hashAlgo, session, sid) ->
                let bHashedMsg = hashedMsg |> Base64StrToBytes
                let bDigiSignature = dSignature |> Base64StrToBytes
                // printfn "%A" bDigiSignature
                let isVerified: bool = verifyDigitalSignature(publicKeys, uid, bHashedMsg, bDigiSignature, hashAlgo)
                let mutable res: string = ""
                match LOGLEVEL with
                | "trace" | "tweets" | "security" ->
                    printfn "Challenge verified! user%A has re-connected" uid
                | _ ->
                    ()
                if isVerified then
                    res <- getTweetsReponse("Connect", uid, [||], -1, "OK", false)
                    if not (isThere(connected, uid)) then
                        connected <- Array.append connected [|uid|]
                        liveSessions.Add(uid, (session, sid))
                else if not isVerified then
                    res <- getTweetsReponse("Connect", uid, [||], -1, "FAIL", false)
                    if (isThere(connected, uid)) then
                        connected <- Array.filter ((<>)uid) connected
                        if liveSessions.ContainsKey(uid) then
                            liveSessions.Remove(uid) |> ignore
                session.SendTo(res, sid)
            | Disconnected uid ->
                connected <- Array.filter ((<>)uid) connected
                if liveSessions.ContainsKey(uid) then
                    let disconnectingSession = fst (liveSessions.[uid])
                    let disconnectingID = snd (liveSessions.[uid])
                    let res =getTweetsReponse("Disconnect", uid, [||], -1, "", false)
                    disconnectingSession.SendTo(res, disconnectingID)
                    // reply("disconnect", uid, disconnectingSession, disconnectingID)
                    liveSessions.Remove(uid) |> ignore
                if not (isThere(disconnected, uid)) then
                    disconnected <- Array.append disconnected [|uid|]
                match LOGLEVEL with
                | "trace" | "tweets" | "security" ->
                    printfn "user%A disconnected" uid
                | _ ->
                    ()    
            | ReceiveTweets (uid, tweet, signed, session, sid) ->
                let mutable signedTweet: string = tweet
                if signed then
                    let uName = userIDs.[uid]
                    signedTweet <- tweet + " [Signed by "+uName+"]"
                match LOGLEVEL with
                | "trace" | "tweets" ->
                    printfn "user%A tweeted: %A" uid signedTweet
                | _ ->
                    ()   
                saveTweetsToDB(uid, signedTweet)
                processHashtagOrMentions(liveSessions, hashtags, &userNames, &userIDs, &connected, uid, signedTweet)
                reply("Tweet", uid, session, sid)
            | ReceiveReTweets (uid, tweet, signed, targetName, session, sid) ->
                let mutable signedTweet: string = tweet
                if signed then
                    let uName = userIDs.[uid]
                    signedTweet <- tweet + " [Signed by "+uName+"]"
                match LOGLEVEL with
                | "trace" | "tweets" ->
                    printfn "user%A retweeted: %A" uid signedTweet
                | _ ->
                    ()    
                saveReTweetsToDB(uid, signedTweet, targetName)
                reply("Retweet", uid, session, sid)
            | SubscribeTo (src, dest, session, sid)->
                subscribe(src, dest)
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A subscribed to user%A" src dest
                | _ ->
                    ()
                reply("Subscribe", src, session, sid)
            | QueryHashtag (uid, hashtag, session, sid) ->
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A queried hashtag: %A" uid hashtag
                | _ ->
                    ()
                replyToQueryHashtag(hashtags, hashtag, uid, session, sid)
            | QueryMentioned (uid, session, sid)  ->
                let dbActor = selectActor("db", string uid)
                dbActor <! Mentioned (uid, session, sid)
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A queried mentions" uid
                | _ -> 
                    ()
            | QuerySubscribed (uid, targetName, session, sid) ->
                querySubscribed(&userNames, targetName, uid, session, sid)
                match LOGLEVEL with
                | "trace" ->
                    printfn "user%A queried sunscribed" uid
                | _ -> 
                    ()
            | CheckConnected (follower, followed, tweet) -> 
                sendToConnected(liveSessions, &connected, follower, userNames.[followed], (followed, tweet))
            // | PrintDebuggingInfo->
            //     printfn "%A" users
            //     for user in users do
            //         let dbActor = selectActor("db", string user)
            //         // dbActor <! PrintUserName
            //         dbActor <! PrintTweets
            | _ ->
                printfn "[Server] Unknown pattern"
            return! loop ()
        } 
        loop ()
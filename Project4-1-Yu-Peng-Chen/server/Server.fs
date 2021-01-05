module Server
    open System
    open System.Collections.Generic
    open TwitterClone
    open Database
    open Akka.Actor
    open Akka.FSharp
    
    let querySubscribed(userNames: byref<Map<string, int>>, targetName: string, userId: int) = 
        if targetName.Length <> 0 then
            let targetId: int = userNames.[targetName]
            let dbActor = selectActor("db", string targetId)
            dbActor <! SendTweets userId //userId: the user requested for the tweets; targetId: the user who's subscribed to
        else
            let dbActor = selectActor("db", string userId)
            dbActor <! LookUpSubscribed userId
            
    let replyToQueryHashtag(hashtags: Dictionary<string, (string * string)[]>, hashtag: string, uid: int) = 
        let localClient = selectLocalActor("client", string uid)
        if hashtags.ContainsKey(hashtag) then
            let tweets: (string * string)[] = hashtags.[hashtag]
            let req = TwitterClone.getReq("QueryHashtagBack", uid, -1, "", tweets)            
            localClient <! req
        else 
            let req = TwitterClone.getReq("QueryHashtagBack", uid, -1, "Hashtag not found!", Array.empty)            
            localClient <! req

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

    let processHashtagOrMentions(hashtags: Dictionary<string, (string * string)[]>, 
                                 userNames: byref<Map<string, int>>, userIDs: byref<Map<int, string>>, 
                                 connected: byref<int []>, uid: int, tweet: string) = 
        let mutable uNames: string[] = Array.empty
        let mutable tags: string[] = Array.empty
        if TwitterClone.checkForHashtagOrMention(tweet, '@') then
            uNames <- extractNameOrTag(tweet, '@')
        if TwitterClone.checkForHashtagOrMention(tweet, '#') then
            tags <- extractNameOrTag(tweet, '#')
         
        for uName in uNames do
            let uNameWithoutSymbol: string = uName.[1..]
            // printfn "[Server] mentioned: %A" uNameWithoutSymbol
            let id: int = userNames.[uNameWithoutSymbol] //id of the mentioned user
            // let dbAct = select ("akka://TwitterClone/user/db" + string id) system
            let dbAct = selectActor("db", string id)
            dbAct <! StoreMentions (userIDs.[uid], tweet) //uid: id of the user who mentions other people
            if isThere(connected, id) then 
                let req = TwitterClone.getReq("LiveMentioned", id, uid, tweet, [|(userIDs.[uid], tweet)|]) 
                let localClient = selectLocalActor("client", string id)
                localClient <! req
        
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
        let dbActor = selectActor("db", string uid)
        dbActor <! StoreReTweets (uid, tweet, targetName)

    let registerUser(users: byref<int []>, 
                     connected: byref<int []>,
                     userNames: byref<Map<string, int>>, 
                     userIDs: byref<Map<int, string>>, 
                     uid: int, uName: string) =
        users <- Array.append users [|uid|]
        connected <- Array.append connected [|uid|]
        // userNames <- Array.append userNames [|uName|]
        userNames <- userNames.Add(uName, uid)
        userIDs <- userIDs.Add(uid, uName)
        let dbActor = spawn system ("db" + string(uid)) dbActor
        // printfn "db%d spawned" uid
        // dbActors <- Array.append dbActors [|dbActor|]
        dbActor <! Init (uid, uName)
        // printfn "[Server] currently registered: %A" userNames
    
    let sendToConnected(connected: byref<int []>, follower: int, followed: int, tweet: (string * string)) =
        if isThere(connected, follower) then 
            let req = TwitterClone.getReq("LiveSubscribed", follower, followed, "", [|tweet|])
            let localClient = selectLocalActor("client", string follower)
            localClient <! req

    let serverActor (serverMailbox:Actor<ServerMsg>) = 
        // let selfActor = serverMailbox.Self
        // let sender = serverMailbox.Sender()
        let mutable users: int [] = Array.empty
        let mutable userNames: Map<string, int> = Map.empty
        let mutable userIDs: Map<int, string> = Map.empty
        let hashtags = new Dictionary<string, (string * string)[]>()
        let mutable deleted: string [] = Array.empty
        let mutable connected: int [] = Array.empty
        let mutable disconnected: int [] = Array.empty
        // let mutable dbActors: IActorRef [] = Array.empty
        let rec loop () = actor {    
            let! (msg: ServerMsg) = serverMailbox.Receive()
            match msg with 
            | Start running->
                printfn "[Server] server is running"
                let req = getReq("Start", -1, -1, "", [|("", "")|])
                localSimulator <! req
            | RegisterAccount (uid, uName) ->
                registerUser(&users, &connected, &userNames, &userIDs, uid, uName)
                reply("RegisterBack", uid)
            | Connected uid ->
                if not (isThere(connected, uid)) then
                    connected <- Array.append connected [|uid|]
                reply("ConnectBack", uid)
                // dbActors.[uid-1] <! LookUpSubscribed uid
            | Disconnected uid ->
                connected <- Array.filter ((<>)uid) connected
                if not (isThere(disconnected, uid)) then
                    disconnected <- Array.append disconnected [|uid|]
                reply("DisconnectBack", uid)
                // printfn "[Server] client%A disconnected!" uid
                // printfn "[Server] connected: %A disconnected: %A" connected disconnected
            | ReceiveTweets (uid, tweet) ->
                saveTweetsToDB(uid, tweet)
                processHashtagOrMentions(hashtags, &userNames, &userIDs, &connected, uid, tweet)
                reply("TweetBack", uid)
            | ReceiveReTweets (uid, tweet, targetName) ->
                saveReTweetsToDB(uid, tweet, targetName)
                reply("RetweetBack", uid)
            | SubscribeTo (src, dest)->
                // printfn("subscribing")
                subscribe(src, dest)
                reply("SubscribeToOneBack", src)
            | QueryHashtag (uid, hashtag) ->
                replyToQueryHashtag(hashtags, hashtag, uid)
            | QueryMentioned uid ->
                let dbActor = selectActor("db", string uid)
                dbActor <! Mentioned uid
            | QuerySubscribed (uid, targetName) ->
                querySubscribed(&userNames, targetName, uid)
            | CheckConnected (follower, followed, tweet) -> 
                sendToConnected(&connected, follower, userNames.[followed], (followed, tweet))
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
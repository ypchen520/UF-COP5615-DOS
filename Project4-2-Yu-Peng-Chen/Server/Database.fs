module Database
    open System
    open Messages
    open Util
    open Akka.Actor
    open Akka.FSharp
    open WebSocketSharp.Server
    open WebSocketSharp    
    
    let sendFollowToDB(dbId: int, uid: int) = 
        // let dbActor = select ("akka://TwitterClone/user/db" + string dbId) system
        let dbActor = selectActor("db", string dbId)
        dbActor <! AddToFollower uid

    let sendTweets(tweets: byref<(string * string) []>, userId: int, targetId: int, session: WebSocketSessionManager, sid: string) =
        //from userId to targetId
        //send userId's tweets to targetId
        if tweets.Length <> 0 then
            let batch: int = int(ceil(double(tweets.Length)/double(10))) //batch size = 10
            let mutable startIdx: int = 0
            let mutable endIdx: int = 0
            for i = 1 to batch do
                if i <> batch then
                    startIdx <- (i-1) * 10
                    endIdx <- i * 10 - 1
                    let partOfTweets = tweets.[startIdx..endIdx]
                    let res = getTweetsReponse("QuerySubscribedRes", userId, partOfTweets, targetId, "", false)
                    session.SendTo(res, sid)
                else
                    let partOfTweets = tweets.[(endIdx+1)..]
                    let res = getTweetsReponse("QuerySubscribedRes", userId, partOfTweets, targetId, "", false)
                    session.SendTo(res, sid)
        else
            let res = getTweetsReponse("QuerySubscribedRes", userId, [||], targetId, "", false) //The client side will show "Not mentioned yet!"
            session.SendTo(res, sid) 

    let storeTweets(tweets: byref<(string * string) []>, uName: string, tweet: string) = 
        tweets <- Array.append tweets [|(uName, tweet)|]

    // let storeMentions(mentions: byref<(string * string) []>, uid: int, tweet: string) = 
    //     mentions <- Array.append mentions [|(uid, tweet)|]    

    let storeReTweets(tweets: byref<(string * string) []>, uName: string, tweet: string, targetName: string) =
        let retweet = "["+targetName+"]"+" "+tweet
        storeTweets(&tweets, uName, retweet)

    let addToFollow(follows: byref<int []>, id: int) = 
        //add to followings or followers
        follows <- Array.append follows [|id|]

    let lookUpSubscribed(followings: byref<int []>, uid: int, session: WebSocketSessionManager, sid: string) = 
        for following in followings do
            // let dbActor = select ("akka://TwitterClone/user/db" + string following) system
            let dbActor = selectActor("db", string following)
            dbActor <! SendTweets (uid, session, sid)

    let sendToFollowers(followers: byref<int []>, userName: string, tweet: string) = 
        // let server = select ("akka://TwitterClone/user/server") system
        let server = selectActor("server", "")
        for follower in followers do
            server <! CheckConnected (follower, userName, tweet)

    let dbActor (dbMailbox:Actor<DBMsg>) = 
        // let selfActor = dbMailbox.Self
        let mutable userName: string = ""
        let mutable userId: int = -1
        let mutable tweets: (string * string) [] = Array.empty
        // let mutable hashtags: string [] = Array.empty
        let mutable mentions: (string * string) [] = Array.empty
        let mutable followings: int [] = Array.empty
        let mutable followers: int [] = Array.empty
        let rec loop () = actor {    
            let! (msg: DBMsg) = dbMailbox.Receive()
            match msg with 
            | Init (uid, uName) -> 
                userName <- uName
                userId <- uid
            | StoreTweets (uid, tweet) ->
                if userId <> uid then
                    printfn "[DB] user%d has not registered yet!" uid
                else
                    storeTweets(&tweets, userName, tweet)
                    if followers.Length <> 0 then
                        sendToFollowers(&followers, userName, tweet)
            | StoreReTweets (uid, tweet, targetName) ->
                if userId <> uid then
                    printfn "[DB] user%d has not registered yet!" uid
                else
                    let retweet = "["+targetName+"]"+" "+tweet
                    // storeReTweets(&tweets, userName, tweet, targetName)
                    storeTweets(&tweets, userName, retweet)
                    if followers.Length <> 0 then
                        sendToFollowers(&followers, userName, retweet)
            | SendTweets (targetId, session, sid) ->
                // send tweets to "targetId" that requested for the tweets
                sendTweets(&tweets, userId, targetId, session, sid)
            | AddToFollowing targetId-> 
                addToFollow(&followings, targetId)
                if userId = -1 then
                    printfn "[DB] user has not registered yet!"
                else
                    sendFollowToDB(targetId, userId)
            | AddToFollower followerId-> 
                addToFollow(&followers, followerId)
            | StoreMentions (uName, tweet) ->
                // uName: the name of the user that tweeted the tweet mentioning this db's owner
                if userId = -1 then
                    printfn "[DB] user%d has not registered yet!" userId
                else    
                    storeTweets(&mentions, uName, tweet)
            | Mentioned (uid, session, sid) ->
                //send to the client itself
                if mentions.Length <> 0 then
                    let tweets: (string * string)[] = mentions
                    let batch: int = int(ceil(double(mentions.Length)/double(10))) //batch size = 10
                    let mutable startIdx: int = 0
                    let mutable endIdx: int = 0
                    for i = 1 to batch do
                        if i <> batch then
                            startIdx <- (i-1) * 10
                            endIdx <- i * 10 - 1
                            let partOfTweets = tweets.[startIdx..endIdx]
                            let res = getTweetsReponse("QueryMentionedRes", uid, partOfTweets, -1, "", false)
                            session.SendTo(res, sid)
                        else
                            let partOfTweets = tweets.[(endIdx+1)..]
                            let res = getTweetsReponse("QueryMentionedRes", uid, partOfTweets, -1, "", false)
                            session.SendTo(res, sid)
                else
                    let res = getTweetsReponse("QueryMentionedRes", uid, [||], -1, "", false) //The client side will show "Not mentioned yet!"
                    session.SendTo(res, sid) 
            | LookUpSubscribed (uid, session, sid)->
                //send to the client (uid, session, sid) queried for the tweets
                lookUpSubscribed(&followings, uid, session, sid)
            // | PrintUserName ->
            //     printfn "[DB%A] %A" userName userName
            // | PrintTweets ->
            //     printfn "[DB%A] %A" userName tweets
            | _ ->
                printfn "[DB] Unknown pattern"
            return! loop ()
        } 
        loop ()
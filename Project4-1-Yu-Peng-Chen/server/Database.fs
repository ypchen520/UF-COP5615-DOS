module Database
    open System
    open TwitterClone
    open Akka.Actor
    open Akka.FSharp    
    
    let sendFollowToDB(dbId: int, uid: int) = 
        // let dbActor = select ("akka://TwitterClone/user/db" + string dbId) system
        let dbActor = selectActor("db", string dbId)
        dbActor <! AddToFollower uid

    let sendTweets(tweets: byref<(string * string) []>, userId: int, targetId: int) =
        //from userId to targetId
        //send userId's tweets to targetId
        let req = getReq("QuerySubscribedBack", userId, targetId, "", tweets)
        let localClient = selectLocalActor("client", string targetId)
        localClient <! req

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

    let lookUpSubscribed(followings: byref<int []>, uid: int) = 
        for following in followings do
            // let dbActor = select ("akka://TwitterClone/user/db" + string following) system
            let dbActor = selectActor("db", string following)
            dbActor <! SendTweets uid

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
                    storeReTweets(&tweets, userName, tweet, targetName)
            | SendTweets targetId ->
                // send tweets to "targetId" that 1. requested for the tweets or 2. connected
                sendTweets(&tweets, userId, targetId)
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
            | Mentioned uid ->
                //send to the client itself
                let localClient = selectLocalActor("client", string uid)
                let req = TwitterClone.getReq("QueryMentionedBack", uid, -1, "", mentions)
                localClient <! req
            | LookUpSubscribed uid->
                //send to the client queried for the tweets
                lookUpSubscribed(&followings, userId)
            // | PrintUserName ->
            //     printfn "[DB%A] %A" userName userName
            // | PrintTweets ->
            //     printfn "[DB%A] %A" userName tweets
            | _ ->
                printfn "[DB] Unknown pattern"
            return! loop ()
        } 
        loop ()
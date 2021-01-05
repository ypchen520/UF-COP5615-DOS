module TwitterClone
    open System
    // open Server
    open Akka.Actor
    open Akka.FSharp
    open Akka.Remote
    open FSharp.Data
    open FSharp.Data.JsonExtensions
    open FSharp.Json
    let system = ActorSystem.Create("TwitterClone")
    let serverSize = 1000

    type DBMsg = 
        | Init of int * string
        | StoreTweets of int * string
        | StoreReTweets of int * string * string
        | SendTweets of int
        | AddToFollowing of int
        | AddToFollower of int
        | StoreMentions of string * string
        | Mentioned of int
        | LookUpSubscribed of int
        | PrintUserName
        | PrintTweets

    type ServerMsg = 
        | Start of bool
        | RegisterAccount of int * string
        | Connected of int
        | Disconnected of int
        | ReceiveTweets of int * string
        | ReceiveReTweets of int * string * string
        | SubscribeTo of int * int
        | QueryHashtag of int * string
        | QueryMentioned of int
        | QuerySubscribed of int * string
        | CheckConnected of int * string * string
        | PrintDebuggingInfo

    let config = """
    akka {  
        actor {
            provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
        }    
        remote.helios.tcp {
            hostname = localhost        
            port = 9001
        }
    }
    """
    // let system = System.create "remote-system" (Configuration.parse config)
    let remoteSystem = System.create "remote-system" (Configuration.parse config)
    let localSystemAddress = "akka.tcp://local-system@localhost:7000"
    let localSimulator = remoteSystem.ActorSelection(localSystemAddress + "/user/LocalHandler")

    let isThere(targets, target): bool = 
        let found = targets |> Seq.exists (fun x -> x = target)
        found

    let checkForHashtagOrMention(tweet: string, symbol: char): bool = 
        isThere(tweet, symbol)
        // tweet |> Seq.exists (fun x -> x = symbol)
    
    let selectActor(actorType: string, id: string): ActorSelection = 
        let actorRef = select ("akka://TwitterClone/user/"+ actorType + id) system
        actorRef

    let selectLocalActor(actorType: string, id: string): ActorSelection = 
        let actorRef = remoteSystem.ActorSelection(localSystemAddress + "/user/" + actorType + id)
        actorRef

    type RecordType = {
        req: string 
        uid: int 
        target: int 
        content: string
        multipleContent: array<string*string>
    }

    let getReq(request: string, userId, targetId, info, infoArr): string =
        let data: RecordType = {
            req = request;
            uid = userId;
            target = targetId;
            content = info;
            multipleContent = infoArr
        }
        let reqString: string = Json.serialize data
        reqString

    let reply(pattern: string, uid: int) =
        let req = getReq(pattern, uid, -1, "", [|("","")|]) 
        let localClient = selectLocalActor("client", string uid)
        localClient <! req    
module TwitterClone
    open System
    // open Server
    open FSharp.Data
    open FSharp.Data.JsonExtensions
    open Akka.Actor
    open Akka.FSharp
    open FSharp.Json
    

    type RecordType = {
        req: string 
        uid: int 
        target: int 
        content: string
        multipleContent: array<string*string>
    }
    
    type SimulatorMsg = 
        | RegisterUser of int
        | SendTweets of int
        | SendTweetsHashtag of int
        | SendTweetsMention of int
        | SendTweetsHashtagMention of int
        | SubscribeToOne of int
        | SubscribeToRand of int
        | QueryHashtag of int
        | QueryMentioned of int
        | QuerySubscribed of int
        | Disconnect of int
        | Connect of int
        | Retweet of int
        | DebuggingInfo of string
        | RequestDone of int
        | Done

    let config = """
    akka {  
        actor {
            provider = "Akka.Remote.RemoteActorRefProvider, Akka.Remote"
        }    
        remote.helios.tcp {
            hostname = localhost        
            port = 7000
        }
    }
    """
    
    let logFoldername = "log"

    let AsString = FSharp.Data.Runtime.JsonConversions.AsString

    let plainTweetArr = [|"What?"; "If that makes sense"; "Make the world a better place"; "Stay safe"; "Keep it real"|]
    let taggedTweetArr = [|"#COP5615 is fun"; "Black life matters #BLM "; 
                           "Stay home! #stayhome "; "Missing home #home "; 
                           "Stay #positive people!"; "But, #COP5615 is challenging"|]
    let mentionedTweetArr = [|"Implementing a Twitter clone is fun!"; 
                              "Let's fight for racial equality!"
                              "Everyone should stay home! right?";
                              "Research is fun! What do you think?"|]                       
    let hashtagsArr = [|"#COP5615"; "#BLM"; "#stayhome"; "#home"; "#positive"|]
   
    let system = ActorSystem.Create("TwitterClone")
    let localSystem = System.create "local-system" (Configuration.parse config)
    let remoteSystemAddress = "akka.tcp://remote-system@localhost:9001"
    let remoteServer = localSystem.ActorSelection(remoteSystemAddress + "/user/RemoteHandler")
    
    let selectActor(actorType: string, id: string): ActorSelection = 
        let actorRef = select ("akka://TwitterClone/user/"+ actorType + id) system
        actorRef

    let getRandNum(upper: int) = 
        let rand = Random()
        let num = rand.Next(upper)
        num
    
    let isThereIndex(content: string, pattern: string): int = 
        let found = content.IndexOf(pattern)
        found

    let isThere(targets, target): bool = 
        let found = targets |> Seq.exists (fun x -> x = target)
        found

    let getMentionTweet(targetName: string) =
        let mentioned = "@" + targetName + " "
        let randIdx = getRandNum(4)
        let mutable tweet: string = ""
        match randIdx with
        | 0 ->
            tweet <- "Implementing a Twitter clone is fun! " + mentioned + "What do you think?"
        | 1 ->
            tweet <- "Let's fight for racial equality! " + mentioned 
        | 2 -> 
            tweet <-mentioned + "Everyone should stay home! right?"
        | 3 -> 
            tweet <-"Research is fun! " + mentioned + "What do you think?"
        | _ ->
            printfn "[getMentionTweet] Unknown pattern"
        tweet

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